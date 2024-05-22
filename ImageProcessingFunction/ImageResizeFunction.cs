using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using SkiaSharp;

public static class ImageProcessingFunction
{
    [FunctionName("ImageProcessingFunction")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        try
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string inputContainerName = "rawImages";
            string outputContainerName = "resizedimages";

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient inputContainerClient = blobServiceClient.GetBlobContainerClient(inputContainerName);
            BlobContainerClient outputContainerClient = blobServiceClient.GetBlobContainerClient(outputContainerName);

            await foreach (BlobItem blobItem in inputContainerClient.GetBlobsAsync())
            {
                BlobClient inputBlobClient = inputContainerClient.GetBlobClient(blobItem.Name);
                BlobDownloadInfo download = await inputBlobClient.DownloadAsync();

                using (MemoryStream ms = new MemoryStream())
                {
                    await download.Content.CopyToAsync(ms);
                    ms.Position = 0;

                    byte[] processedImageData = GenerateThumbnail(ms.ToArray());

                    BlobClient outputBlobClient = outputContainerClient.GetBlobClient(blobItem.Name);
                    using (MemoryStream outputStream = new MemoryStream(processedImageData))
                    {
                        await outputBlobClient.UploadAsync(outputStream, true);
                    }

                    using (MemoryStream emptyStream = new MemoryStream())
                    {
                        await inputBlobClient.UploadAsync(emptyStream, overwrite: true);
                    }
                }
            }

            return new OkObjectResult("Processed all images.");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "An error occurred while processing images.");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    private static byte[] GenerateThumbnail(byte[] imageData)
    {
        using (var inputStream = new MemoryStream(imageData))
        using (var original = SKBitmap.Decode(inputStream))
        {
            int thumbnailWidth = 100;
            int thumbnailHeight = (int)((thumbnailWidth / (double)original.Width) * original.Height);

            using (var resized = original.Resize(new SKImageInfo(thumbnailWidth, thumbnailHeight), SKFilterQuality.High))
            using (var image = SKImage.FromBitmap(resized))
            using (var outputStream = new MemoryStream())
            {
                image.Encode(SKEncodedImageFormat.Jpeg, 100).SaveTo(outputStream);
                return outputStream.ToArray();
            }
        }
    }
}

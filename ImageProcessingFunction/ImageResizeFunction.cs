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
            string inputContainerName = "rawimages";
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

                    await inputBlobClient.DeleteIfExistsAsync();

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
        if (imageData == null || imageData.Length == 0)
            throw new ArgumentNullException(nameof(imageData), "Image data cannot be null or empty.");

        using (var inputStream = new MemoryStream(imageData))
        using (var original = SKBitmap.Decode(inputStream))
        {
            if (original == null)
                throw new InvalidOperationException("Failed to decode the input image.");

            using (var surface = SKSurface.Create(new SKImageInfo(300, 300)))
            {
                var canvas = surface.Canvas;

                canvas.Clear(SKColors.Transparent);

                var paint = new SKPaint();
                canvas.DrawBitmap(original, SKRect.Create(0, 0, 300, 300), paint);

                paint.Color = SKColors.Red;
                paint.TextSize = 20;
                canvas.DrawText("cloud computing", 10, 30, paint);

                using (var image = surface.Snapshot())
                using (var encodedData = image.Encode())
                {
                    return encodedData.ToArray();
                }
            }
        }
    }
}

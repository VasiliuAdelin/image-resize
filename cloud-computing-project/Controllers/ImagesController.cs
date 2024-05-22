using cloud_computing_project.Helpers;
using cloud_computing_project.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace cloud_computing_project.Controllers
{
    [Route("api/[controller]")]
    public class ImagesController : Controller
    {
        private readonly AzureStorageConfig storageConfig = null;
        private readonly ILogger<ImagesController> _logger;

        public ImagesController(IOptions<AzureStorageConfig> config, ILogger<ImagesController> logger)
        {
            storageConfig = config.Value;
            _logger = logger;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Upload(ICollection<IFormFile> files)
        {
            bool isUploaded = false;

            try
            {
                if (files.Count == 0)
                {
                    _logger.LogWarning("No files received from the upload.");
                    return BadRequest("No files received from the upload");
                }

                if (string.IsNullOrEmpty(storageConfig.AccountKey) || string.IsNullOrEmpty(storageConfig.AccountName))
                {
                    _logger.LogError("Azure storage details are missing in appsettings.js.");
                    return BadRequest("Sorry, can't retrieve your Azure storage details from appsettings.js, make sure that you add Azure storage details there");
                }

                if (string.IsNullOrEmpty(storageConfig.ImageContainer))
                {
                    _logger.LogError("Image container name is missing in Azure blob storage.");
                    return BadRequest("Please provide a name for your image container in Azure blob storage");
                }

                foreach (var formFile in files)
                {
                    if (StorageHelper.IsImage(formFile))
                    {
                        if (formFile.Length > 0)
                        {
                            using (Stream stream = formFile.OpenReadStream())
                            {
                                isUploaded = await StorageHelper.UploadFileToStorage(stream, formFile.FileName, storageConfig);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Unsupported media type: {FileName}", formFile.FileName);
                        return new UnsupportedMediaTypeResult();
                    }
                }

                if (isUploaded)
                {
                    if (!string.IsNullOrEmpty(storageConfig.ThumbnailContainer))
                        return new AcceptedAtActionResult("GetThumbNails", "Images", null, null);
                    else
                        return new AcceptedResult();
                }
                else
                {
                    _logger.LogError("Image upload to storage failed.");
                    return BadRequest("Looks like the image couldn't upload to the storage");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception caught during image upload.");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("thumbnails")]
        public async Task<IActionResult> GetThumbNails()
        {
            try
            {
                if (string.IsNullOrEmpty(storageConfig.AccountKey) || string.IsNullOrEmpty(storageConfig.AccountName))
                {
                    _logger.LogError("Azure storage details are missing in appsettings.js.");
                    return BadRequest("Sorry, can't retrieve your Azure storage details from appsettings.js, make sure that you add Azure storage details there.");
                }

                if (string.IsNullOrEmpty(storageConfig.ImageContainer))
                {
                    _logger.LogError("Image container name is missing in Azure blob storage.");
                    return BadRequest("Please provide a name for your image container in Azure blob storage.");
                }

                List<string> thumbnailUrls = await StorageHelper.GetThumbNailUrls(storageConfig);
                return new ObjectResult(thumbnailUrls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception caught during getting thumbnails.");
                return BadRequest(ex.Message);
            }
        }
    }
}

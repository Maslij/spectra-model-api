using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Spectra.Model.Api.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;
using Spectra.Model.Api.Models.Pascal;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Spectra.Model.Api.Helpers;
using System.Threading;

namespace Spectra.Model.Api.Services
{
    public static class Extensions
    {
        public static T[] Append<T>(this T[] array, T item)
        {
            if (array == null)
            {
                return new T[] { item };
            }

            T[] result = new T[array.Length + 1];
            for (int i = 0; i < array.Length; i++)
            {
                result[i] = array[i];
            }

            result[array.Length] = item;
            return result;
        }
    }
    public class CustomVisionService
    {

        private readonly AzureStorageConfiguration _azureStorageConfig;
        private CustomVisionTrainingClient trainingApi;
        private CloudBlobContainer cloudBlobContainer;
        private CloudBlobClient blobClient;

        private readonly ILogger _logger;
        private IMongoDatabase _database;

        public CustomVisionService(IOptions<AzureStorageConfiguration> azureStorageConfig, ISpectraDatabaseSettings settings, ILogger<CustomVisionService> logger)
        {
            _azureStorageConfig = azureStorageConfig.Value;
            _logger = logger;

            var client = new MongoClient(settings.ConnectionString);
            _database = client.GetDatabase(settings.DatabaseName);
        }

        private static async Task UploadFile(CloudBlockBlob blob, string path)
        {
            using (var fileStream = System.IO.File.OpenRead(path))
            {
                await blob.UploadFromStreamAsync(fileStream);
            }
        }

        private CloudBlobClient InitiateBlobClient()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_azureStorageConfig.ConnectionString);
            return storageAccount.CreateCloudBlobClient();
        }

        private CustomVisionTrainingClient AuthenticateTraining(string endpoint, string trainingKey)
        {
            // Create the Api, passing in the training key
            CustomVisionTrainingClient trainingApi = new CustomVisionTrainingClient(new Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.ApiKeyServiceClientCredentials(trainingKey))
            {
                Endpoint = endpoint
            };
            return trainingApi;
        }
        private CustomVisionPredictionClient AuthenticatePrediction(string endpoint, string predictionKey)
        {
            // Create a prediction endpoint, passing in the obtained prediction key
            CustomVisionPredictionClient predictionApi = new CustomVisionPredictionClient(new Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.ApiKeyServiceClientCredentials(predictionKey))
            {
                Endpoint = endpoint
            };
            return predictionApi;
        }

        public async Task<IList<SpectraProjectWithMetadata>> GetProjects(CustomVisionProject project)
        {
            CustomVisionTrainingClient trainingApi = AuthenticateTraining(project.Endpoint, project.TrainingKey);

            var spectraProjectCollection = _database.GetCollection<SpectraProject>("spectra-projects");
            var projectMetaData = spectraProjectCollection.Find(project => true).ToList();

            // Get all the Custom Vision projects
            var customVisionProjects = await trainingApi.GetProjectsAsync();

            IList<SpectraProjectWithMetadata> spectraProjects = new List<SpectraProjectWithMetadata>();
            IList<Project> metaDataMatches = new List<Project>();

            List<SpectraProjectWithMetadata> mergedList =
                projectMetaData.Join(
                        customVisionProjects,
                        x1 => Guid.Parse(x1.ProjectId),
                        x2 => x2.Id,
                        (x1, x2) => new SpectraProjectWithMetadata
                        {
                            Category = x1.Category,
                            DemoUrls = x1.DemoUrls,
                            Created = x2.Created,
                            Description = x2.Description,
                            DrModeEnabled = x2.DrModeEnabled,
                            Id = x2.Id,
                            LastModified = x2.LastModified,
                            Name = x2.Name,
                            Settings = x2.Settings,
                            Status = x2.Status,
                            ThumbnailUri = x2.ThumbnailUri
                        })
                        .ToList();

            return mergedList;
        }

        public async Task<SpectraProjectWithMetadata> GetProject(CustomVisionProject project, string projectId)
        {
            CustomVisionTrainingClient trainingApi = AuthenticateTraining(project.Endpoint, project.TrainingKey);

            var spectraProjectCollection = _database.GetCollection<SpectraProject>("spectra-projects");
            
            var projectMetaData = spectraProjectCollection.Find(x => x.ProjectId == projectId).FirstOrDefault();

            // Get all the Custom Vision projects
            var customVisionProject = await trainingApi.GetProjectAsync(Guid.Parse(projectId));

            var mergedObject = new SpectraProjectWithMetadata
            {
                Category = projectMetaData.Category,
                DemoUrls = projectMetaData.DemoUrls,
                Created = customVisionProject.Created,
                Description = customVisionProject.Description,
                DrModeEnabled = customVisionProject.DrModeEnabled,
                Id = customVisionProject.Id,
                LastModified = customVisionProject.LastModified,
                Name = customVisionProject.Name,
                Settings = customVisionProject.Settings,
                Status = customVisionProject.Status,
                ThumbnailUri = customVisionProject.ThumbnailUri
            };

            return mergedObject;
        }

        public static bool DoesFileExist(string fileName, CloudBlobClient cloudBlobClient, string containerReference)
        {
            return cloudBlobClient.GetContainerReference(containerReference).GetBlockBlobReference(fileName).Exists();
        }

        public static Uri GetBlobUrl(string fileName, CloudBlobClient cloudBlobClient, string containerReference)
        {
            return cloudBlobClient.GetContainerReference(containerReference).GetBlockBlobReference(fileName).Uri;
        }
        
        public async Task<object> UploadImageToAzure(CustomVisionBatchImage customVisionBatchImage, Guid projectId)
        {
            CustomVisionTrainingClient trainingApi = AuthenticateTraining(customVisionBatchImage.Endpoint, customVisionBatchImage.TrainingKey);
            var currentProject = await trainingApi.GetProjectAsync(projectId);

            // Create the ImageUrlBatch to send to CustomVision
            List<ImageUrlCreateEntry> singleUrlEntry = new List<ImageUrlCreateEntry>();

            foreach(CustomVisionImage customVisionImage in customVisionBatchImage.BatchImage)
            {
                singleUrlEntry.Add(new ImageUrlCreateEntry(customVisionImage.ImageUri));
            }

            ImageUrlCreateBatch batchEntry = new ImageUrlCreateBatch(singleUrlEntry);
            var isUploaded = trainingApi.CreateImagesFromUrlsAsync(projectId, batchEntry);
            List<string> createdImageUris = new List<string>();
            
            foreach (ImageCreateResult image in isUploaded.Result.Images)
            {
                createdImageUris.Add(image.Image.OriginalImageUri);
            }

            var response = new
            {
                project_id = projectId,
                project_name = currentProject.Name,
                image_count = isUploaded.Result.Images.Count,
                image_create_result = createdImageUris
            };

            return response;

        }

        private async Task<CloudBlobContainer> FindOrCreateBlob(CloudBlobClient blobClient, Guid projectId)
        {
            CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference(projectId.ToString());

            // Create the container if it doesn't exist
            if (!cloudBlobContainer.Exists())
            {
                await cloudBlobContainer.CreateAsync();
                var containerPermissions = new BlobContainerPermissions();
                containerPermissions.PublicAccess = BlobContainerPublicAccessType.Blob;
                cloudBlobContainer.SetPermissions(containerPermissions);
            }
            return cloudBlobContainer;
        }

        private async Task<IList<Image>> GetProjectWithImagesAndRegions(Guid projectId, Guid iterationId)
        {
            var imageCount = await trainingApi.GetTaggedImageCountAsync(projectId, iterationId);
            IList<Image> projectWithImagesAndRegions = new List<Image>();

            for (int i = 0; i < imageCount; i = i + 100)
            {
                var diff = Math.Abs((decimal)(i - imageCount));
                if (diff < 100)
                {
                    var smallSplitProjectWithImagesAndRegions = await trainingApi.GetTaggedImagesAsync(projectId, iterationId: iterationId, take: (int?)diff, skip: i);
                    projectWithImagesAndRegions = projectWithImagesAndRegions.Concat(smallSplitProjectWithImagesAndRegions).ToList();
                }
                else
                {
                    var splitProjectWithImagesAndRegions = await trainingApi.GetTaggedImagesAsync(projectId, iterationId: iterationId, take: 100, skip: i);
                    projectWithImagesAndRegions = projectWithImagesAndRegions.Concat(splitProjectWithImagesAndRegions).ToList();
                }
            }

            return projectWithImagesAndRegions;
        }

        public async Task<bool> ExtractRegionsFromCustomVisionImage(IList<Image> projectWithImagesAndRegions, Guid projectId, string convertTo)
        {
            // File metadata
            var _path = Path.GetTempPath();
            var _startPath = $"{_path}/Images";

            // Set a counter for logging purposes
            int count = 0;
            foreach (Image image in projectWithImagesAndRegions)
            {
                _logger.LogInformation($"[INFO] Extracting Regions from Image {count}/{projectWithImagesAndRegions.Count()}");
                var _imageFileName = $"{image.Id}.jpg";
                var _imagePath = $"{_startPath}/{_imageFileName}";

                _logger.LogInformation($"[INFO] Downloading File");
                // Download the Image URL
                /*using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(image.OriginalImageUri, _imagePath);
                }*/

                Downloader BlobDownloader = new Downloader();

                BlobDownloader.DownloadFile(image.OriginalImageUri, _imagePath);

                while (!BlobDownloader.DownloadCompleted)
                    Thread.Sleep(1000);

                CloudBlockBlob cloudBlockBlobImage = cloudBlobContainer.GetBlockBlobReference(_imageFileName);

                _logger.LogInformation($"[INFO] Checking if the Image file exists.");
                // Check if the file already exits in the blob, if it doesn't, upload it.
                bool imageUrl = DoesFileExist(_imageFileName, blobClient, projectId.ToString());
                if (!imageUrl)
                    await cloudBlockBlobImage.UploadFromFileAsync(_imagePath);

                //bool didImageFileUpload = await UploadBlob($"{image.Id}.jpg", _startPath, projectId);

                string fileExtension = "";
                switch (convertTo)
                {
                    case "pascal":
                        fileExtension = ".xml";
                        Annotation pascalAnnotationObject = ConvertAnnotationsToPascal(image);
                        bool didCreatePascalFile = CreatePascalAnnotationFile(pascalAnnotationObject, $"{_startPath}/{image.Id}{fileExtension}");
                        bool didPascalFileUpload = await UploadBlob($"{image.Id}.xml", _startPath, projectId);
                        break;
                    case "customvision":
                        fileExtension = ".json";
                        // No need to convert the annotations.
                        bool didCreateCustomVisionFile = await CreateCustomVisionAnnotationFile(image, $"{_startPath}/{image.Id}{fileExtension}");
                        bool didCustomVisionFileUpload = await UploadBlob($"{image.Id}.json", _startPath, projectId);
                        break;
                    case "yolo":
                        fileExtension = ".txt";
                        List<string> yoloAnnotationList = ConvertAnnotationsToYolo(image);
                        bool didCreateYoloFile = CreateYoloAnnotationFile(yoloAnnotationList, $"{_startPath}/{image.Id}{fileExtension}");
                        bool didYoloFileUpload = await UploadBlob($"{image.Id}.txt", _startPath, projectId);
                        break;
                }
                count++;
            }

            return true;
        }

        private List<string> ConvertAnnotationsToYolo(Image image)
        {
            var _txtFileName = $"{image.Id}.txt";
            List<string> imageObjects = new List<string>();

            if (image.Regions != null)
            {
                foreach (ImageRegion region in image.Regions)
                {
                    double x_centre = region.Left + (region.Width / 2);
                    double y_centre = region.Top + (region.Height / 2);
                    double x_width = region.Width;
                    double y_height = region.Height;

                    imageObjects.Add($"{region.TagId} {x_centre} {y_centre} {x_width} {y_height}");
                }
            }

            return imageObjects;

        }

            private Annotation ConvertAnnotationsToPascal(Image image)
        {
            List<Models.Pascal.Object> imageObjects = new List<Models.Pascal.Object>();
            var _xmlFileName = $"{image.Id}.xml";

            if (image.Regions != null)
            {
                foreach (ImageRegion region in image.Regions)
                {

                    BoundBox newBounds = new BoundBox
                    {
                        Xmin = region.Left * region.Width,
                        Ymin = region.Top * region.Height,
                        Xmax = region.Width * region.Width,
                        Ymax = region.Height * region.Height
                    };
                    imageObjects.Add(new Models.Pascal.Object
                    {
                        Name = region.TagName,
                        Bndbox = newBounds
                    });
                }
            }

            // Create the Pascal Annotation
            var pascalAnnotationFile = new Annotation
            {

                Folder = "",
                FileName = _xmlFileName,
                Source = new Source { Database = "" },
                Size = new Size
                {
                    Depth = 3,
                    Width = image.Width,
                    Height = image.Height,
                },
                Objects = imageObjects
            };

            return pascalAnnotationFile;
        }

        private async Task<bool> CreateCustomVisionAnnotationFile(Image image, string _jsonPath)
        {
            // Create the JSON Annotation file
            using Stream writer = new FileStream(_jsonPath, FileMode.OpenOrCreate);
            {
                await JsonSerializer.SerializeAsync(writer, image);
                writer.Close();
            }
            return true;
        }

        private bool CreateYoloAnnotationFile(List<string> yoloAnnotationList, string _txtPath)
        {
            using (StreamWriter file =
                new StreamWriter(_txtPath))
            {
                foreach (string line in yoloAnnotationList)
                    file.WriteLine(line);
            }
            return true;
        }

        private bool CreatePascalAnnotationFile(Annotation pascalAnnotationFile, string _xmlPath)
        {
            try
            {
                XmlSerializer x = new System.Xml.Serialization.XmlSerializer(pascalAnnotationFile.GetType());

                using Stream writer = new FileStream(_xmlPath, FileMode.OpenOrCreate);
                {
                    x.Serialize(writer, pascalAnnotationFile);
                    writer.Close();
                }

                // Fix the formatting
                /*XElement xmlDoc = XElement.Load(_xmlPath);
                XElement nodeToRemove = xmlDoc.Element("Objects");
                var childNodes = nodeToRemove.Elements();
                nodeToRemove.Remove();
                xmlDoc.Add(childNodes);

                using Stream newWriter = new FileStream(_xmlPath, FileMode.OpenOrCreate);
                {
                    xmlDoc.Save(newWriter);
                    newWriter.Close();
                }*/
                return true;
            }
            catch(Exception e)
            {
                return false;
            }
        }

        public async Task<bool> UploadBlob(string fileName, string filePath, Guid projectId)
        {
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);
            await cloudBlockBlob.UploadFromFileAsync($"{filePath}/{fileName}");

            return true;
        }

        public async Task<object> GetProjectWithImageAndAnnotations(CustomVisionProject project, Guid projectId, Guid iterationId, string convertTo)
        {
            try
            {
                // Set the training API
                trainingApi = AuthenticateTraining(project.Endpoint, project.TrainingKey);

                // Set the blob client
                blobClient = InitiateBlobClient();

                // Set the container if it doesn't exist
                cloudBlobContainer = await FindOrCreateBlob(blobClient, projectId);

                // Get the current Project
                var currentProject = await trainingApi.GetProjectAsync(projectId);

                // Get the images and regions of the current project and iteration
                IList<Image> projectWithImagesAndRegions = await GetProjectWithImagesAndRegions(projectId, iterationId);

                // Set the file metadata
                var _path = Path.GetTempPath();
                var _startPath = $"{_path}/Images";
                var _zippedPath = $"{_path}/Zip";
                string _fileName = $"{projectId}-{iterationId}-{convertTo}.zip";

                // Does the Zip file already exist?
                bool zipUrl = DoesFileExist(_fileName, blobClient, projectId.ToString());
                if (zipUrl)
                {
                    CloudBlockBlob cloudZipBlob = cloudBlobContainer.GetBlockBlobReference(_fileName);
                    return new
                    {
                        project_id = projectId,
                        project_name = currentProject.Name,
                        image_count = projectWithImagesAndRegions.Count(),
                        zipped_project = cloudZipBlob.Uri
                    };
                }

                _logger.LogInformation($"[INFO] Creating Temp Directory at {_startPath}");

                // Create the Temp directory if it doesn't exist
                if (!Directory.Exists(_startPath))
                    Directory.CreateDirectory(_startPath);

                // Extract the Custom Vision regions from each individual image.
                bool successfullExtraction = await ExtractRegionsFromCustomVisionImage(projectWithImagesAndRegions, projectId, convertTo);
            


                // Finally, zip the directory.
                _logger.LogInformation($"[INFO] Zipping Project to {_startPath}{_zippedPath}");
                string zippedPath = ZipAndUploadDirectory(_startPath, $"{_zippedPath}/{_fileName}");

                // Upload the zip file to Azure
                Uri zippedFileUri = await UploadBlobToAzure(cloudBlobContainer, zippedPath, _fileName);

                // Clean-up
                DirectoryInfo dInfo = new DirectoryInfo(_startPath);
                foreach (FileInfo file in dInfo.GetFiles())
                    file.Delete();

                dInfo = new DirectoryInfo(_zippedPath);
                foreach (FileInfo file in dInfo.GetFiles())
                    file.Delete();

                // Generate Response
                var response = new
                {
                    project_id = projectId,
                    project_name = currentProject.Name,
                    image_count = projectWithImagesAndRegions.Count(),
                    zipped_project = zippedFileUri
                };


                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"[ERROR]  {e.Message}");
                return new
                {
                    error = e.Message
                };
            }
        }

        public async Task<Uri> UploadBlobToAzure(CloudBlobContainer cloudBlobContainer, string filePath, string fileName)
        {
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);
            await cloudBlockBlob.UploadFromFileAsync(filePath);
            return cloudBlobContainer.GetBlockBlobReference(fileName).Uri;
        }

        public string ZipAndUploadDirectory(string startPath, string zipPath)
        {
            using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(startPath))
                {
                    var entryName = Path.GetFileName(file);
                    var entry = archive.CreateEntry(entryName);
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var stream = entry.Open())
                    {
                        fs.CopyTo(stream, 81920);
                    }
                }
            }

            //byte[] fileBytes = System.IO.File.ReadAllBytes(_zipPath);

            return zipPath;
        }
    }
}

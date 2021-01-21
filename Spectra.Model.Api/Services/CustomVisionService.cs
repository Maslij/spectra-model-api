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

        public CustomVisionService(IOptions<AzureStorageConfiguration> azureStorageConfig)
        {
            _azureStorageConfig = azureStorageConfig.Value;
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

        public async Task<IList<Project>> GetProjects(CustomVisionProject project)
        {
            CustomVisionTrainingClient trainingApi = AuthenticateTraining(project.Endpoint, project.TrainingKey);
            return await trainingApi.GetProjectsAsync();
        }

        public async Task<Project> GetProject(CustomVisionProject project, Guid projectId)
        {
            CustomVisionTrainingClient trainingApi = AuthenticateTraining(project.Endpoint, project.TrainingKey);
            return await trainingApi.GetProjectAsync(projectId);
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

        public async Task<object> GetProjectWithImageAndPascalAnnotations(CustomVisionProject project, Guid projectId, Guid iterationId)
        {
            CustomVisionTrainingClient trainingApi = AuthenticateTraining(project.Endpoint, project.TrainingKey);
            CloudBlobClient blobClient = InitiateBlobClient();

            string containerReference = $"{projectId}";
            CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference(containerReference);

            // Create the container if it doesn't exist
            if (!cloudBlobContainer.Exists())
            {
                await cloudBlobContainer.CreateAsync();
                var containerPermissions = new BlobContainerPermissions();
                containerPermissions.PublicAccess = BlobContainerPublicAccessType.Blob;
                cloudBlobContainer.SetPermissions(containerPermissions);
            }
            var imageCount = await trainingApi.GetTaggedImageCountAsync(projectId, iterationId);
            var currentProject = await trainingApi.GetProjectAsync(projectId);
            var projectWithImagesAndRegions = await trainingApi.GetImagesAsync(projectId, iterationId: iterationId, take: imageCount);

            int count = 0;
            var _path = Path.GetTempPath();
            var _startPath = $"{_path}/Images";
            var _zippedPath = $"{_path}/Zip";
            string _fileName = $"{projectId}.zip";
            //string _zipPath = $"{_path}/Images/{_fileName}";

            if (!Directory.Exists(_startPath))
                Directory.CreateDirectory(_startPath);
            Dictionary<string, object> _blobDirectory = new Dictionary<string, object>();

            foreach (Image image in projectWithImagesAndRegions)
            {
                var _xmlFileName = $"{image.Id}.xml";
                var _imageFileName = $"{image.Id}.jpg";
                var _xmlPath = $"{_startPath}/{_xmlFileName}";
                var _imagePath = $"{_startPath}/{_imageFileName}";


                // Download the Image URL
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(image.OriginalImageUri, _imagePath);
                }
                CloudBlockBlob cloudBlockBlobImage = cloudBlobContainer.GetBlockBlobReference(_imageFileName);

                // Check if the file already exits in the blob
                bool imageUrl = DoesFileExist(_imageFileName, blobClient, containerReference);
                if (!imageUrl)
                    await cloudBlockBlobImage.UploadFromFileAsync(_imagePath);

                List<Models.Pascal.Object> imageObjects = new List<Models.Pascal.Object>();

                if(image.Regions != null)
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

                // Create the PASCAL VOC Annotation file

                XmlSerializer x = new System.Xml.Serialization.XmlSerializer(pascalAnnotationFile.GetType());

                using Stream writer = new FileStream(_xmlPath, FileMode.OpenOrCreate);
                {
                    x.Serialize(writer, pascalAnnotationFile);
                    writer.Close();
                }

                // Fix the formatting
                XElement xmlDoc = XElement.Load(_xmlPath);
                XElement nodeToRemove = xmlDoc.Element("Objects");
                var childNodes = nodeToRemove.Elements();
                nodeToRemove.Remove();
                xmlDoc.Add(childNodes);

                using Stream newWriter = new FileStream(_xmlPath, FileMode.OpenOrCreate);
                {
                    xmlDoc.Save(newWriter);
                    newWriter.Close();
                }

                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(_xmlFileName);
                await cloudBlockBlob.UploadFromFileAsync(_xmlPath);
                Uri jsonUrl = GetBlobUrl(_xmlFileName, blobClient, containerReference);

                var imageLinks = new
                {
                    image_uri = cloudBlockBlobImage.Uri,
                    annotations_uri = cloudBlockBlob.Uri
                };

                _blobDirectory.Add($"image_{count}", imageLinks);

                count++;
            }
            // Finally, zip the directory.
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

            var response = new
            {
                project_id = projectId,
                project_name = currentProject.Name,
                image_count = count,
                zipped_project = zippedFileUri
            };


            return response;
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

        public async Task<object> GetProjectWithImagesAndRegions(CustomVisionProject project, Guid projectId, Guid iterationId)
        {
            CustomVisionTrainingClient trainingApi = AuthenticateTraining(project.Endpoint, project.TrainingKey);
            CloudBlobClient blobClient = InitiateBlobClient();
            CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference($"{projectId}");

            // Create the container if it doesn't exist
            if (!cloudBlobContainer.Exists())
            {
                await cloudBlobContainer.CreateAsync();
                var containerPermissions = new BlobContainerPermissions();
                containerPermissions.PublicAccess = BlobContainerPublicAccessType.Blob;
                cloudBlobContainer.SetPermissions(containerPermissions);
            }
            var imageCount = await trainingApi.GetTaggedImageCountAsync(projectId, iterationId);
            var currentProject = await trainingApi.GetProjectAsync(projectId);
            var projectWithImagesAndRegions = await trainingApi.GetImagesAsync(projectId, iterationId: iterationId, take: imageCount);

            int count = 0;
            var _path = Path.GetTempPath();
            var _startPath = $"{_path}/Images";
            var _zippedPath = $"{_path}/Zip";
            string _fileName = $"{projectId}.zip";
            //string _zipPath = $"{_path}/Images/{_fileName}";

            if (!Directory.Exists(_startPath))
                Directory.CreateDirectory(_startPath);
            if (!Directory.Exists(_zippedPath))
                Directory.CreateDirectory(_zippedPath);
            Dictionary<string, object> _blobDirectory = new Dictionary<string, object>();

            foreach (Image image in projectWithImagesAndRegions)
            {
                var _jsonFileName = $"{image.Id}.json";
                var _imageFileName = $"{image.Id}.jpg";
                var _jsonPath = $"{_startPath}/{_jsonFileName}";
                var _imagePath = $"{_startPath}/{_imageFileName}";


                // Download the Image URL
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(image.OriginalImageUri, _imagePath);
                }
                CloudBlockBlob cloudBlockBlobImage = cloudBlobContainer.GetBlockBlobReference(_imageFileName);
                await cloudBlockBlobImage.UploadFromFileAsync(_imagePath);

                // Create the JSON Annotation file
                using Stream writer = new FileStream(_jsonPath, FileMode.OpenOrCreate);
                {
                    await JsonSerializer.SerializeAsync(writer, image);
                    writer.Close();
                }
                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(_jsonFileName);
                await cloudBlockBlob.UploadFromFileAsync(_jsonPath);

                var imageLinks = new
                {
                    image_uri = cloudBlockBlobImage.Uri,
                    annotations_uri = cloudBlockBlob.Uri
                };

                _blobDirectory.Add($"image_{count}", imageLinks);

                count++;
            }

            // Finally, zip the directory.
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

            var response = new
            {
                project_id = projectId,
                project_name = currentProject.Name,
                image_count = count,
                zipped_project = zippedFileUri
            };

            return response;
            /*using (FileStream zipToOpen = new FileStream(_zipPath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(_startPath))
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

            byte[] fileBytes = System.IO.File.ReadAllBytes(_zipPath);

            return fileBytes;
            */
        }
    }
}

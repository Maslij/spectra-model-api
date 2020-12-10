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

namespace Spectra.Model.Api.Services
{
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

        public async Task<object> GetZippedProjectWithImagesAndRegions(CustomVisionProject project, Guid projectId)
        {
            CustomVisionTrainingClient trainingApi = AuthenticateTraining(project.Endpoint, project.TrainingKey);
            CloudBlobClient blobClient = InitiateBlobClient();
            CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference($"spectra-{Guid.NewGuid()}");
            await cloudBlobContainer.CreateAsync();
            var containerPermissions = new BlobContainerPermissions();
            containerPermissions.PublicAccess = BlobContainerPublicAccessType.Blob;
            cloudBlobContainer.SetPermissions(containerPermissions);

            var currentProject = await trainingApi.GetProjectAsync(projectId);
            var projectWithImagesAndRegions = await trainingApi.GetImagesAsync(projectId);

            int count = 0;
            var _path = Path.GetTempPath();
            var _startPath = $"{_path}/Images";
            string _fileName = $"{projectId}.zip";
            //string _zipPath = $"{_path}/Images/{_fileName}";

            if (!Directory.Exists(_startPath))
                Directory.CreateDirectory(_startPath);
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

            var response = new
            {
                project_id = projectId,
                project_name = currentProject.Name,
                image_count = count,
                project_images = _blobDirectory
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

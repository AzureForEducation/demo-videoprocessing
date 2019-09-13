using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.WebJobs.Host;
using VideoProcessing.Services;
using VideoProcessing.Entities;

namespace VideoProcessing
{
    public static class A_InitialSetupGenerator
    {
        // AD auth variables
        static readonly string _tenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
        static readonly string _restApiUrl = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");
        static readonly string _clientId = Environment.GetEnvironmentVariable("AMSClientId");
        static readonly string _clientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");
        static readonly string _storageConnection = Environment.GetEnvironmentVariable("StorageAccountConnection");

        [FunctionName("A_InitialSetupGenerator")]
        public static async Task<object> GeneratesInitialSetup([ActivityTrigger] VideoAMS videoDto, TraceWriter log)
        {
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            Asset asset = new Asset();
            Locator locator = new Locator();
            string accessPolicyId = "";

            // Getting the service authenticated
            MediaServices mediaService = new MediaServices(_tenantDomain, restApiUrl: _restApiUrl, _clientId, _clientSecret, _storageConnection);
            try
            {
                await mediaService.InitializeAccessTokenAsync();
                log.Info("Authenication... Done.");
            }
            catch (Exception ex)
            {
                return httpResponse.RequestMessage.CreateResponse(HttpStatusCode.Unauthorized, $"It wasn't possible get the service authenticated. \n {ex.StackTrace}");
            }

            // Creating asset and locator
            try
            {
                // Creating the asset
                accessPolicyId = await mediaService.GenerateAccessPolicy(videoDto.AccessPolicyName, 100, 2);
                asset = await mediaService.GenerateAsset(videoDto.AssetName, videoDto.StorageAccountName);
                log.Info("Asset creation... Done.");

                // Creating the locator
                locator = await mediaService.GenerateLocator(accessPolicyId, asset.Id, DateTime.Now.AddMinutes(-5), 1);
                log.Info("Locator creation... Done.");
            }
            catch (Exception ex)
            {
                return httpResponse.RequestMessage.CreateResponse(HttpStatusCode.InternalServerError, $"It wasn't possible to create the asset. \n {ex.StackTrace}");
            }

            // Moving the original video into the asset
            try
            {
                // Converting string path into Uri
                Uri uriSource = new Uri(videoDto.VideoPath, UriKind.Absolute);

                // Moving file into the asset
                CloudBlockBlob sourceBlob;
                sourceBlob = new CloudBlockBlob(uriSource);
                await MediaServices.MoveVideoToAssetLocator(sourceBlob, locator, _storageConnection);
                await mediaService.GenerateFileInfo(asset.Id);

                log.Info("Moving video... Done.");
            }
            catch (Exception ex)
            {
                return httpResponse.RequestMessage.CreateResponse(HttpStatusCode.InternalServerError, $"It wasn't possible to upload the video. \n {ex.StackTrace}");
            }

            return new InitialSetupResult {
                Asset = asset,
                Locator = locator,
                Video = videoDto
            };
        }
    }
}

/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using Autodesk.Forge;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Forge.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Text;

using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;
using Alias = Autodesk.Forge.DesignAutomation.Model.Alias;
using AppBundle = Autodesk.Forge.DesignAutomation.Model.AppBundle;
using Parameter = Autodesk.Forge.DesignAutomation.Model.Parameter;
using WorkItem = Autodesk.Forge.DesignAutomation.Model.WorkItem;
using WorkItemStatus = Autodesk.Forge.DesignAutomation.Model.WorkItemStatus;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Autodesk.Forge.Core;
using Microsoft.Extensions.Options;

namespace forgeSample.Controllers
{
    [ApiController]
    public class DesignAutomationController : ControllerBase
    {
        // Used to access the application folder (temp location for files & bundles)
        private IHostingEnvironment _env;
        // used to access the SignalR Hub
        private IHubContext<DesignAutomationHub> _hubContext;
        // Local folder for bundles
        public string LocalBundlesFolder { get { return Path.Combine(_env.WebRootPath, "bundles"); } }
        /// Prefix for AppBundles and Activities
        public static string NickName { get { return OAuthController.GetAppSetting("FORGE_CLIENT_ID"); } }
        /// Alias for the app (e.g. DEV, STG, PROD). This value may come from an environment variable
        public static string Alias { get { return "dev"; } }
        // Design Automation v3 API
        DesignAutomationClient _designAutomation;

        public static string bucketUrl = "https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}";

        // Constructor, where env and hubContext are specified
        public DesignAutomationController(IHostingEnvironment env, IHubContext<DesignAutomationHub> hubContext, DesignAutomationClient api)
        {
            _designAutomation = api;
            _env = env;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Get all Activities defined for this account
        /// </summary>
        [HttpGet]
        [Route("/api/forge/oauth/public")]
        public async Task<IActionResult> GetAuth()
        {
            dynamic oauth = await OAuthController.GetInternalAsync();
            //List<string> response = new List<string>();
            //response.Add(oauth.access_token);
            //string expires = new string(oauth.expires_in);
            //response.Add(expires);

            return Ok(new { access_token = oauth.access_token, expires_in = oauth.expires_in });
            //return response;
        }

        /// <summary>
        /// Get all Activities defined for this account
        /// </summary>
        [HttpGet]
        [Route("api/forge/designautomation/activities")]
        public async Task<List<string>> GetDefinedActivities()
        {
            // filter list of 
            Page<string> activities = await _designAutomation.GetActivitiesAsync();
            List<string> definedActivities = new List<string>();
            foreach (string activity in activities.Data)
                if (activity.StartsWith(NickName) && activity.IndexOf("$LATEST") == -1)
                    definedActivities.Add(activity.Replace(NickName + ".", String.Empty));

            return definedActivities;
        }

        /// <summary>
        /// Define a new activity
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/activities")]
        public async Task<IActionResult> CreateActivity([FromBody]JObject activitySpecs)
        {
            // basic input validation
            string zipFileName = activitySpecs["zipFileName"].Value<string>();
            string engineName = activitySpecs["engine"].Value<string>();

            // standard name for this sample
            string appBundleName = zipFileName + "AppBundle";
            string activityName = zipFileName + "Activity";

            // 
            Page<string> activities = await _designAutomation.GetActivitiesAsync();
            string qualifiedActivityId = string.Format("{0}.{1}+{2}", NickName, activityName, Alias);
            if (!activities.Data.Contains(qualifiedActivityId))
            {
                // define the activity
                // ToDo: parametrize for different engines...
                dynamic engineAttributes = EngineAttributes(engineName);
                string commandLine = string.Format(engineAttributes.commandLine, appBundleName);
                Activity activitySpec = new Activity()
                {
                    Id = activityName,
                    Appbundles = new List<string>() { string.Format("{0}.{1}+{2}", NickName, appBundleName, Alias) },
                    CommandLine = new List<string>() { commandLine },
                    Engine = engineName,
                    Parameters = new Dictionary<string, Parameter>()
                    {
                        { "inputFile", new Parameter() { Description = "input file", LocalName = "$(inputFile)", Ondemand = false, Required = true, Verb = Verb.Get, Zip = false } },
                        { "inputJson", new Parameter() { Description = "input json", LocalName = "params.json", Ondemand = false, Required = false, Verb = Verb.Get, Zip = false } },
                        { "outputFile", new Parameter() { Description = "output file", LocalName = "outputFile." + engineAttributes.extension, Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } }
                    },
                    Settings = new Dictionary<string, ISetting>()
                    {
                        { "script", new StringSetting(){ Value = engineAttributes.script } }
                    }
                };
                Activity newActivity = await _designAutomation.CreateActivityAsync(activitySpec);

                // specify the alias for this Activity
                Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                Alias newAlias = await _designAutomation.CreateActivityAliasAsync(activityName, aliasSpec);

                return Ok(new { Activity = qualifiedActivityId });
            }

            // as this activity points to a AppBundle "dev" alias (which points to the last version of the bundle),
            // there is no need to update it (for this sample), but this may be extended for different contexts
            return Ok(new { Activity = "Activity already defined" });
        }

        /// <summary>
        /// Helps identify the engine
        /// </summary>
        private dynamic EngineAttributes(string engine)
        {
            if (engine.Contains("3dsMax")) return new { commandLine = @"$(engine.path)\\3dsmaxbatch.exe -sceneFile $(args[inputFile].path) $(settings[script].path)", extension = "max", script = "da = dotNetClass(\"Autodesk.Forge.Sample.DesignAutomation.Max.RuntimeExecute\")\nda.ModifyWindowWidthHeight()\n" };
            if (engine.Contains("AutoCAD")) return new { commandLine = "$(engine.path)\\accoreconsole.exe /i $(args[inputFile].path) /al $(appbundles[{0}].path) /s $(settings[script].path)", extension = "dwg", script = "UpdateParam\n" };
            if (engine.Contains("Inventor")) return new { commandLine = "$(engine.path)\\InventorCoreConsole.exe /i $(args[inputFile].path) /al $(appbundles[{0}].path)", extension = "ipt", script = string.Empty };
            if (engine.Contains("Revit")) return new { commandLine = "$(engine.path)\\revitcoreconsole.exe /i $(args[inputFile].path) /al $(appbundles[{0}].path)", extension = "rvt", script = string.Empty };
            throw new Exception("Invalid engine");
        }

        /// <summary>
        /// Define a new appbundle
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/appbundles")]
        public async Task<IActionResult> CreateAppBundle([FromBody]JObject appBundleSpecs)
        {
            // basic input validation
            string zipFileName = appBundleSpecs["zipFileName"].Value<string>();
            string engineName = appBundleSpecs["engine"].Value<string>();

            // standard name for this sample
            string appBundleName = zipFileName + "AppBundle";

            // check if ZIP with bundle is here
            string packageZipPath = Path.Combine(LocalBundlesFolder, zipFileName + ".zip");
            if (!System.IO.File.Exists(packageZipPath)) throw new Exception("Appbundle not found at " + packageZipPath);

            // get defined app bundles
            Page<string> appBundles = await _designAutomation.GetAppBundlesAsync();

            // check if app bundle is already define
            dynamic newAppVersion;
            string qualifiedAppBundleId = string.Format("{0}.{1}+{2}", NickName, appBundleName, Alias);
            if (!appBundles.Data.Contains(qualifiedAppBundleId))
            {
                // create an appbundle (version 1)
                AppBundle appBundleSpec = new AppBundle()
                {
                    Package = appBundleName,
                    Engine = engineName,
                    Id = appBundleName,
                    Description = string.Format("Description for {0}", appBundleName),

                };
                newAppVersion = await _designAutomation.CreateAppBundleAsync(appBundleSpec);
                if (newAppVersion == null) throw new Exception("Cannot create new app");

                // create alias pointing to v1
                Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                Alias newAlias = await _designAutomation.CreateAppBundleAliasAsync(appBundleName, aliasSpec);
            }
            else
            {
                // create new version
                AppBundle appBundleSpec = new AppBundle()
                {
                    Engine = engineName,
                    Description = appBundleName
                };
                newAppVersion = await _designAutomation.CreateAppBundleVersionAsync(appBundleName, appBundleSpec);
                if (newAppVersion == null) throw new Exception("Cannot create new version");

                // update alias pointing to v+1
                AliasPatch aliasSpec = new AliasPatch()
                {
                    Version = newAppVersion.Version
                };
                Alias newAlias = await _designAutomation.ModifyAppBundleAliasAsync(appBundleName, Alias, aliasSpec);
            }

            // upload the zip with .bundle
            RestClient uploadClient = new RestClient(newAppVersion.UploadParameters.EndpointURL);
            RestRequest request = new RestRequest(string.Empty, Method.POST);
            request.AlwaysMultipartFormData = true;
            foreach (KeyValuePair<string, string> x in newAppVersion.UploadParameters.FormData) request.AddParameter(x.Key, x.Value);
            request.AddFile("file", packageZipPath);
            request.AddHeader("Cache-Control", "no-cache");
            await uploadClient.ExecuteTaskAsync(request);

            return Ok(new { AppBundle = qualifiedAppBundleId, Version = newAppVersion.Version });
        }

        /// <summary>
        /// Input for StartWorkitem
        /// </summary>
        //public class StartWorkitemInput
        //{
        //  public IFormFile inputFile { get; set; }
        //  public string data { get; set; }
        //}

        /// <summary>
        /// Start a new workitem
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/workitems/extractparams")]
        public async Task<IActionResult> ExtractParams([FromBody]JObject workItemsSpecs)
        {
            // basic input validation
            string documentPath = workItemsSpecs["documentPath"].Value<string>();
            string projectPath = workItemsSpecs["projectPath"].Value<string>();
            string inputFile = workItemsSpecs["inputFile"].Value<string>();
            string outputFile = inputFile + ".json";
            string viewableFile = "viewable.zip";
            string browerConnectionId = workItemsSpecs["browerConnectionId"].Value<string>();
            string activityName = string.Format("{0}.{1}", NickName, "ExtractParams+alpha");

            string bucketKey = NickName.ToLower() + "_designautomation";

            // OAuth token
            dynamic oauth = await OAuthController.GetInternalAsync();

            // prepare workitem arguments
            // 1. input file
            XrefTreeArgument inputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, inputFile),
                Headers = new Dictionary<string, string>()
                 {
                     { "Authorization", "Bearer " + oauth.access_token }
                 }
            };
            // 2. input json
            dynamic inputJson = new JObject();
            inputJson.documentPath = documentPath;
            if (projectPath != "")
                inputJson.projectPath = projectPath;
            XrefTreeArgument inputJsonArgument = new XrefTreeArgument()
            {
                Url = "data:application/json, " + ((JObject)inputJson).ToString(Formatting.None).Replace("\"", "'")
            };
            // 3. output file
            XrefTreeArgument outputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, outputFile),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };
            // 4. Viewable output
            XrefTreeArgument viewableArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, viewableFile),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
           {
                 {"Authorization", "Bearer " + oauth.access_token }
           }
            };

            // prepare & submit workitem
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/extractparams?id={1}&outputFileName={2}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, HttpUtility.UrlEncode(outputFile));
            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = activityName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputFile", inputFileArgument },
                    { "inputParams", inputJsonArgument },
                    { "documentParams", outputFileArgument },
                    { "viewable", viewableArgument },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };
            try
            {
                WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemsAsync(workItemSpec);
                return Ok(new { WorkItemId = workItemStatus.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }



        }

        /// <summary>
        /// Start a new workitem
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/workitems/updatemodel")]
        public async Task<IActionResult> UpdateModel([FromBody]JObject workItemsSpecs)
        {
            // basic input validation
            string inputFileValue = workItemsSpecs["file"].Value<string>();
            string projectPathValue = workItemsSpecs["projectPath"].Value<string>();
            string documentPathValue = workItemsSpecs["documentPath"].Value<string>();
            string browerConnectionId = workItemsSpecs["browerConnectionId"].Value<string>();

            string inputFileNameOSS = inputFileValue;
            string outputFileNameOSS = "viewable.zip";
            string outputAssemblyFileNameOSS = "result.zip";

            // OAuth token
            dynamic oauth = await OAuthController.GetInternalAsync();

            // upload file to OSS Bucket
            // 1. ensure bucket existis
            string bucketKey = NickName.ToLower() + "_designautomation";

            // prepare workitem arguments
            // 1. input file
            XrefTreeArgument inputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, inputFileNameOSS),
                Headers = new Dictionary<string, string>()
                 {
                     { "Authorization", "Bearer " + oauth.access_token }
                 }
            };
            // 2. input params json 
            JObject inputParams = JObject.FromObject(new { inputFile = documentPathValue, projectFile = projectPathValue, outputType = "svf" });

            XrefTreeArgument inputParamsArgument = new XrefTreeArgument()
            {
                Url = "data:application/json, " + ((JObject)inputParams).ToString(Formatting.None).Replace("\"", "'")
            };
            // 2. document params json
            dynamic dpocumentParametersJson = workItemsSpecs["parameters"];

            XrefTreeArgument documentParamsArgument = new XrefTreeArgument()
            {
                Url = "data:application/json, " + ((JObject)dpocumentParametersJson).ToString(Formatting.None).Replace("\"", "'")
            };
            // 3. output svf file
            XrefTreeArgument outputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, outputFileNameOSS),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };
            // 3. output assembly file
            XrefTreeArgument outputAssemblyFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, outputAssemblyFileNameOSS),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            string activityName = NickName + ".UpdateUserParameters+alpha";

            // prepare & submit workitem
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/updatemodel?id={1}&outputFileName={2}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, outputFileNameOSS);
            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = activityName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputFile", inputFileArgument },
                    { "inputParams",  inputParamsArgument },
                    { "documentParams",  documentParamsArgument },
                    { "outputViewable", outputFileArgument },
                    { "outputAssembly", outputAssemblyFileArgument},
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };
            WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemsAsync(workItemSpec);

            return Ok(new { WorkItemId = workItemStatus.Id });
        }
        /// <summary>
        /// Start a new workitem
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/workitems/updatedrawing")]
        public async Task<IActionResult> UpdateDrawing([FromBody]JObject workItemsSpecs)
        {
            // basic input validation
            string inputFileValue = workItemsSpecs["file"].Value<string>();
            string projectPathValue = workItemsSpecs["projectPath"].Value<string>();
            string documentPathValue = workItemsSpecs["documentPath"].Value<string>();
            string documentRunRuleValue = workItemsSpecs["runRule"].Value<string>();
            string documentDrawingDocValue = workItemsSpecs["drawingDocName"].Value<string>();
            string browerConnectionId = workItemsSpecs["browerConnectionId"].Value<string>();

            string inputFileNameOSS = inputFileValue;
            string outputPdfFileNameOSS = "result.pdf";
            string outputDrawingFileNameOSS = "result.idw";

            // OAuth token
            dynamic oauth = await OAuthController.GetInternalAsync();

            // upload file to OSS Bucket
            // 1. ensure bucket existis
            string bucketKey = NickName.ToLower() + "_designautomation";

            // prepare workitem arguments
            // 1. input file
            XrefTreeArgument inputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, inputFileNameOSS),
                Headers = new Dictionary<string, string>()
                 {
                     { "Authorization", "Bearer " + oauth.access_token }
                 }
            };
            // 2. input params json 
            JObject inputParams = JObject.FromObject(new { inputFile = documentPathValue, projectFile = projectPathValue, runRule = documentRunRuleValue, drawingDocName = documentDrawingDocValue });

            XrefTreeArgument inputParamsArgument = new XrefTreeArgument()
            {
                Url = "data:application/json, " + ((JObject)inputParams).ToString(Formatting.None).Replace("\"", "'")
            };

            // 3. output pdf file
            XrefTreeArgument outputDrawingFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, outputDrawingFileNameOSS),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            // 3. output pdf file
            XrefTreeArgument outputPdfFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, outputPdfFileNameOSS),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            string activityName = NickName + ".CreateDrawing+alpha";

            // prepare & submit workitem
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/updatedrawing?id={1}&outputPdfFileName={2}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, outputPdfFileNameOSS);
            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = activityName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputFile", inputFileArgument },
                    { "inputParams",  inputParamsArgument },
                    { "outputDrawing", outputDrawingFileArgument },
                    { "outputPdf", outputPdfFileArgument },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };
            WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemsAsync(workItemSpec);

            return Ok(new { WorkItemId = workItemStatus.Id });
        }

        /// <summary>
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation/extractparams")]
        public async Task<IActionResult> OnCallbackExractParams(string id, string outputFileName, [FromBody]dynamic body)
        {
            try
            {
                // your webhook should return immediately! we can use Hangfire to schedule a job
                //await UpdateViewable(id, outputFileName, "onParameters", "viewable.zip" , body);

                // your webhook should return immediately! we can use Hangfire to schedule a job
                JObject bodyJson = JObject.Parse((string)body.ToString());
                await _hubContext.Clients.Client(id).SendAsync("onComplete", bodyJson.ToString());

                var client = new RestClient(bodyJson["reportUrl"].Value<string>());
                var request = new RestRequest(string.Empty);

                byte[] bs = client.DownloadData(request);
                string report = System.Text.Encoding.Default.GetString(bs);
                await _hubContext.Clients.Client(id).SendAsync("onComplete", report);

                // Read parameters from json result file
                ObjectsApi objectsApi = new ObjectsApi();
                dynamic parameters = await objectsApi.GetObjectAsyncWithHttpInfo(NickName.ToLower() + "_designautomation", outputFileName);
                string data;
                using (StreamReader reader = new StreamReader(parameters.Data))
                {
                    data = reader.ReadToEnd();
                }

                await _hubContext.Clients.Client(id).SendAsync("onParameters", data);

                // Get LMV viewable and host on web server
                string viewable = "viewable.zip";
                string filePath = "wwwroot/viewables/" + viewable;
                using (System.IO.Stream viewableStream = objectsApi.GetObject(NickName.ToLower() + "_designautomation", viewable))
                using (FileStream fileFile = System.IO.File.Create(filePath))
                {
                    viewableStream.CopyTo(fileFile);
                }
                string viewableDir = "wwwroot/viewables/viewable";
                // Delete the old viewable if it exists (to optimize this cache by configuration)
                if (Directory.Exists(viewableDir))
                {
                    Directory.Delete(viewableDir, true);
                }

                // Unzip the viewable
                ZipFile.ExtractToDirectory(filePath, viewableDir);
                await _hubContext.Clients.Client(id).SendAsync("onViewableUpdate", "");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

            // ALWAYS return ok (200)
            return Ok();
        }

        /// <summary>
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation/updatebom")]
        public async Task<IActionResult> OnCallbackUpdateBom(string id, string outputFileName, [FromBody]dynamic body)
        {
            try
            {
                JObject bodyJson = JObject.Parse((string)body.ToString());
                await _hubContext.Clients.Client(id).SendAsync("onComplete", bodyJson.ToString());

                var client = new RestClient(bodyJson["reportUrl"].Value<string>());
                var request = new RestRequest(string.Empty);

                byte[] bs = client.DownloadData(request);
                string report = System.Text.Encoding.Default.GetString(bs);
                await _hubContext.Clients.Client(id).SendAsync("onComplete", report);

                ObjectsApi objectsApi = new ObjectsApi();
                dynamic parameters = await objectsApi.GetObjectAsyncWithHttpInfo(NickName.ToLower() + "_designautomation", outputFileName);
                string data;
                using (StreamReader reader = new StreamReader(parameters.Data))
                    data = reader.ReadToEnd();

                await _hubContext.Clients.Client(id).SendAsync("onBom", data);

            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

            // ALWAYS return ok (200)
            return Ok();
        }

        /// <summary>
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation/updatedrawing")]
        public async Task<IActionResult> OnCallbackUpdateDrawing(string id, string outputFileName, [FromBody]dynamic body)
        {
            try
            {
                //await UpdateViewable(id, outputFileName, "onDrawing", "result.pdf", body);
                // your webhook should return immediately! we can use Hangfire to schedule a job
                JObject bodyJson = JObject.Parse((string)body.ToString());
                await _hubContext.Clients.Client(id).SendAsync("onComplete", bodyJson.ToString());

                var client = new RestClient(bodyJson["reportUrl"].Value<string>());
                var request = new RestRequest(string.Empty);

                byte[] bs = client.DownloadData(request);
                string report = System.Text.Encoding.Default.GetString(bs);
                await _hubContext.Clients.Client(id).SendAsync("onComplete", report);

                // Get LMV viewable and host on web server
                string viewable = "result.pdf";
                string filePath = "wwwroot/viewables/" + viewable;

                ObjectsApi objectsApi = new ObjectsApi();
                using (System.IO.Stream viewableStream = objectsApi.GetObject(NickName.ToLower() + "_designautomation", viewable))
                using (FileStream fileFile = System.IO.File.Create(filePath))
                {
                    viewableStream.CopyTo(fileFile);
                }

                await _hubContext.Clients.Client(id).SendAsync("onDrawing", "");

            }
            catch (Exception e) 
            {
                Console.WriteLine("Error: " + e.Message);
            }

            // ALWAYS return ok (200)
            return Ok();
        }

        /// <summary>
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation/updatemodel")]
        public async Task<IActionResult> OnCallbackUpdateModel(string id, string outputFileName, [FromBody]dynamic body)
        {
            //try
            //{
            //    await UpdateViewable(id, outputFileName, "onModelUpdate", "result.zip", body);
            //}
            //catch (Exception e) { }
            try
            {
                // your webhook should return immediately! we can use Hangfire to schedule a job
                JObject bodyJson = JObject.Parse((string)body.ToString());
                await _hubContext.Clients.Client(id).SendAsync("onComplete", bodyJson.ToString());

                var client = new RestClient(bodyJson["reportUrl"].Value<string>());
                var request = new RestRequest(string.Empty);

                byte[] bs = client.DownloadData(request);
                string report = System.Text.Encoding.Default.GetString(bs);
                await _hubContext.Clients.Client(id).SendAsync("onComplete", report);

                //// Read parameters from json result file
                //// Todo: only do this if we need to update the parameters
                ObjectsApi objectsApi = new ObjectsApi();
                //dynamic parameters = await objectsApi.GetObjectAsyncWithHttpInfo(NickName.ToLower() + "_designautomation", outputFileName);
                //string data;
                //using (StreamReader reader = new StreamReader(parameters.Data))
                //{
                //    data = reader.ReadToEnd();
                //}

                await _hubContext.Clients.Client(id).SendAsync("onModelUpdate", "");

                // Get LMV viewable and host on web server
                string viewable = "viewable.zip";
                string filePath = "wwwroot/viewables/" + viewable;

                using (System.IO.Stream viewableStream = objectsApi.GetObject(NickName.ToLower() + "_designautomation", viewable))
                using (FileStream fileFile = System.IO.File.Create(filePath))
                {
                    viewableStream.CopyTo(fileFile);
                }

                string viewableDir = "wwwroot/viewables/viewable";
                // Delete the old viewable if it exists (to optimize this cache by configuration)
                if (Directory.Exists(viewableDir))
                {
                    Directory.Delete(viewableDir, true);
                }

                // Unzip the viewable
                ZipFile.ExtractToDirectory(filePath, viewableDir);
                await _hubContext.Clients.Client(id).SendAsync("onViewableUpdate", "");

            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

            // ALWAYS return ok (200)
            return Ok();
        }

        /// <summary>
        /// Start a new workitem
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/workitems/updatebom")]
        public async Task<IActionResult> UpdateBOM([FromBody]JObject workItemsSpecs)
        {
            // basic input validation
            string inputFileValue = workItemsSpecs["file"].Value<string>();
            string projectPathValue = workItemsSpecs["projectPath"].Value<string>();
            string documentPathValue = workItemsSpecs["documentPath"].Value<string>();
            string browerConnectionId = workItemsSpecs["browerConnectionId"].Value<string>();

            string inputFileNameOSS = inputFileValue;

            string outputFileNameOSS = "bomRows.json";

            // OAuth token
            dynamic oauth = await OAuthController.GetInternalAsync();

            // upload file to OSS Bucket
            // 1. ensure bucket existis
            string bucketKey = NickName.ToLower() + "_designautomation";

            // prepare workitem arguments
            // 1. input file
            XrefTreeArgument inputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, inputFileNameOSS),
                Headers = new Dictionary<string, string>()
                 {
                     { "Authorization", "Bearer " + oauth.access_token }
                 }
            };
            // 2. input params json 
            JObject inputParams = JObject.FromObject(new { inputFile = documentPathValue, projectFile = projectPathValue });

            XrefTreeArgument inputParamsArgument = new XrefTreeArgument()
            {
                Url = "data:application/json, " + ((JObject)inputParams).ToString(Formatting.None).Replace("\"", "'")
            };

            // 3. output file
            XrefTreeArgument outputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(bucketUrl, bucketKey, outputFileNameOSS),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            string activityName = NickName + ".UpdateBom+alpha";

            // prepare & submit workitem
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/updatebom?id={1}&outputFileName={2}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, outputFileNameOSS);
            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = activityName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputFile", inputFileArgument },
                    { "inputParams",  inputParamsArgument },
                    { "outputFile", outputFileArgument },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };
            WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemsAsync(workItemSpec);

            return Ok(new { WorkItemId = workItemStatus.Id });
        }

        /// <summary>
        /// Start a new workitem
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/workitems/getrfa")]
        public async Task<IActionResult> GetRfa([FromBody]JObject workItemsSpecs)
        {
            // basic input validation
            string documentPath = workItemsSpecs["documentPath"].Value<string>();
            string inputFile = workItemsSpecs["inputFile"].Value<string>();
            string outputFile = inputFile + ".sat";
            string browerConnectionId = workItemsSpecs["browerConnectionId"].Value<string>();
            string activityName = string.Format("{0}.{1}", NickName, "InventorSatExport+alpha");

            string bucketKey = NickName.ToLower() + "_designautomation";

            // OAuth token
            dynamic oauth = await OAuthController.GetInternalAsync();

            // prepare workitem arguments
            // input file
            XrefTreeArgument inputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, inputFile),
                PathInZip = documentPath.Split('/', 2)[1],
                LocalName = documentPath.Split('/', 2)[0],
                Headers = new Dictionary<string, string>()
                 {
                     { "Authorization", "Bearer " + oauth.access_token }
                 }
            };

            // output file
            XrefTreeArgument outputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, outputFile),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            // prepare & submit workitem
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/inventorsatexport?id={1}&outputFileName={2}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, HttpUtility.UrlEncode(outputFile));
            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = activityName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "InventorDoc", inputFileArgument },
                    { "OutputSat", outputFileArgument },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };
            try
            {
                WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemsAsync(workItemSpec);
                return Ok(new { WorkItemId = workItemStatus.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation/inventorsatexport")]
        public async Task<IActionResult> OnCallbackInventorSatExport(string id, string outputFileName, [FromBody]dynamic body)
        {
            try
            {
                // your webhook should return immediately! we can use Hangfire to schedule a job
                JObject bodyJson = JObject.Parse((string)body.ToString());
                await _hubContext.Clients.Client(id).SendAsync("onProgress", bodyJson.ToString());

                var client = new RestClient(bodyJson["reportUrl"].Value<string>());
                var request = new RestRequest(string.Empty);

                byte[] bs = client.DownloadData(request);
                string report = System.Text.Encoding.Default.GetString(bs);
                await _hubContext.Clients.Client(id).SendAsync("onProgress", report);

                // start Sat2Rfa Workitem
                // basic input validation
                // string documentPath = workItemsSpecs["documentPath"].Value<string>();
                // string inputFile = workItemsSpecs["inputFile"].Value<string>();
                string outputFile = Path.ChangeExtension(outputFileName, ".rfa");
                string activityName = string.Format("{0}.{1}", NickName, "Sat2Revit+alpha");  // TODO

                string bucketKey = NickName.ToLower() + "_designautomation";

                // OAuth token
                dynamic oauth = await OAuthController.GetInternalAsync();

                // prepare workitem arguments
                // input file
                XrefTreeArgument inputFileArgument = new XrefTreeArgument()
                {
                    Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, outputFileName),
                    Headers = new Dictionary<string, string>()
                    {
                        { "Authorization", "Bearer " + oauth.access_token }
                    }
                };

                // revit template
                XrefTreeArgument revitTemplateArgument = new XrefTreeArgument()
                {
                    Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, "RevitTemplate"),
                    Headers = new Dictionary<string, string>()
                    {
                        { "Authorization", "Bearer " + oauth.access_token }
                    }
                };

                // output file
                XrefTreeArgument outputFileArgument = new XrefTreeArgument()
                {
                    Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, outputFile),
                    Verb = Verb.Put,
                    Headers = new Dictionary<string, string>()
                    {
                        {"Authorization", "Bearer " + oauth.access_token }
                    }
                };

                await _hubContext.Clients.Client(id).SendAsync("onProgress", "Preparing sat2Revit workitem...");
                // prepare & submit workitem
                string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/revitrfaexport?id={1}&outputFileName={2}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), id, HttpUtility.UrlEncode(outputFile));
                WorkItem workItemSpec = new WorkItem()
                {
                    ActivityId = activityName,
                    Arguments = new Dictionary<string, IArgument>()
                    {
                        { "InputGeometry", inputFileArgument },
                        { "FamilyTemplate", revitTemplateArgument },
                        { "ResultModel", outputFileArgument },
                        { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                    }
                };
                try
                {
                    WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemsAsync(workItemSpec);
                    await _hubContext.Clients.Client(id).SendAsync("onProgress", $"Workitem started: {workItemStatus.Id}");
                    return Ok(new { WorkItemId = workItemStatus.Id });
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
            // ALWAYS return ok (200)
            return Ok();
        }

        /// <summary>
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation/revitrfaexport")]
        public async Task<IActionResult> OnCallbackRevitRfaExport(string id, string outputFileName, [FromBody]dynamic body)
        {
            // TOOD: slightly modify to provide rfa download link
            try
            {
                // your webhook should return immediately! we can use Hangfire to schedule a job
                //await UpdateViewable(id, outputFileName, "onParameters", "viewable.zip" , body);

                // your webhook should return immediately! we can use Hangfire to schedule a job
                JObject bodyJson = JObject.Parse((string)body.ToString());
                await _hubContext.Clients.Client(id).SendAsync("onComplete", bodyJson.ToString());

                var client = new RestClient(bodyJson["reportUrl"].Value<string>());
                var request = new RestRequest(string.Empty);

                byte[] bs = client.DownloadData(request);
                string report = System.Text.Encoding.Default.GetString(bs);
                await _hubContext.Clients.Client(id).SendAsync("onComplete", report);

                // Read parameters from json result file
                ObjectsApi objectsApi = new ObjectsApi();
                dynamic parameters = await objectsApi.GetObjectAsyncWithHttpInfo(NickName.ToLower() + "_designautomation", outputFileName);
                string data;
                using (StreamReader reader = new StreamReader(parameters.Data))
                {
                    data = reader.ReadToEnd();
                }

                await _hubContext.Clients.Client(id).SendAsync("onParameters", data);

                // Get LMV viewable and host on web server
                string viewable = "viewable.zip";
                string filePath = "wwwroot/viewables/" + viewable;
                using (System.IO.Stream viewableStream = objectsApi.GetObject(NickName.ToLower() + "_designautomation", viewable))
                using (FileStream fileFile = System.IO.File.Create(filePath))
                {
                    viewableStream.CopyTo(fileFile);
                }
                string viewableDir = "wwwroot/viewables/viewable";
                // Delete the old viewable if it exists (to optimize this cache by configuration)
                if (Directory.Exists(viewableDir))
                {
                    Directory.Delete(viewableDir, true);
                }

                // Unzip the viewable
                ZipFile.ExtractToDirectory(filePath, viewableDir);
                await _hubContext.Clients.Client(id).SendAsync("onViewableUpdate", "");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

            // ALWAYS return ok (200)
            return Ok();
        }

        /// <summary>
        /// Return a list of available engines
        /// </summary>
        [HttpGet]
        [Route("api/forge/designautomation/engines")]
        public async Task<List<string>> GetAvailableEngines()
        {
            dynamic oauth = await OAuthController.GetInternalAsync();

            // define Engines API
            Page<string> engines = await _designAutomation.GetEnginesAsync();
            engines.Data.Sort();

            return engines.Data; // return list of engines
        }

        /// <summary>
        /// Return a list of available engines
        /// </summary>
        [HttpGet]
        [Route("api/forge/datamanagement/objects")]
        public async Task<ActionResult<IList<string>>> GetFileInBucket()
        {
            dynamic oauth = await OAuthController.GetInternalAsync();

            // define Engines API
            string bucketKey = NickName.ToLower() + "_designautomation";
            ObjectsApi objects = new ObjectsApi();
            objects.Configuration.AccessToken = oauth.access_token;

            try
            {
                dynamic response = await objects.GetObjectsAsyncWithHttpInfo(bucketKey);
                IDictionary<string, dynamic> dict = response.Data.Dictionary["items"].Dictionary;
                List<string> list = new List<string>();
                foreach (dynamic item in dict)
                    list.Add(item.Value.Dictionary["objectKey"]);

                return Ok(list); // return list of objects in bucket
            }
            catch
            {
                return BadRequest();
            }
        }

        /// <summary>
        /// Clear the accounts (for debugging purposes)
        /// </summary>
        [HttpDelete]
        [Route("api/forge/designautomation/account")]
        public async Task<IActionResult> ClearAccount()
        {
            // clear account
            await _designAutomation.DeleteForgeAppAsync("me");
            return Ok();
        }

        /// <summary>
        /// Names of app bundles on this project
        /// </summary>
        [HttpPost]
        [Route("api/forge/signedurl")]
        public async Task<IActionResult> GetSignedUrl([FromBody]JObject inputData)
        {
            string file = inputData["file"].Value<string>();
            string signedUrlValue = "";
            try
            {
                //string file = urlParams["file"].Value<string>();
                //string file = "result.zip";
                dynamic oauth = await OAuthController.GetInternalAsync();

                // define Engines API
                string bucketKey = NickName.ToLower() + "_designautomation";
                ObjectsApi objects = new ObjectsApi();
                objects.Configuration.AccessToken = oauth.access_token;

                var postBucketsSigned = new PostBucketsSigned(30); // PostBucketsSigned | Body Structure

                // this folder is placed under the public folder, which may expose the bundles
                // but it was defined this way so it be published on most hosts easily
                //dynamic response = await objects.CreateSignedResource(bucketKey, file, postBucketsSigned, "read");
                var result = objects.CreateSignedResource(bucketKey, file, postBucketsSigned, "read");
                signedUrlValue = result.signedUrl;

            }
            catch (Exception e) 
            {
                Console.WriteLine("Error: " + e.Message);
            }
            return Ok(new { signedurl = signedUrlValue }); // return list of objects in bucket

        }

        /// <summary>
        /// Names of app bundles on this project
        /// </summary>
        [HttpGet]
        [Route("api/appbundles")]
        public string[] GetLocalBundles()
        {
            // this folder is placed under the public folder, which may expose the bundles
            // but it was defined this way so it be published on most hosts easily
            return Directory.GetFiles(LocalBundlesFolder, "*.zip").Select(Path.GetFileNameWithoutExtension).ToArray();
        }
    }

    /// <summary>
    /// Class uses for SignalR
    /// </summary>
    public class DesignAutomationHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public string GetConnectionId() { return Context.ConnectionId; }
    }

}
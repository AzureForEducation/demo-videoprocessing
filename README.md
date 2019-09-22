# Video Processing Workflow

This repository brings a implementation of a complete video processing workflow which takes advantage of the following Azure resources to perform ingestion, encoding:

* **Azure Media Services (AMS)**: Azure Media Services is a cloud-based media workflow platform that enables you to build solutions that require encoding, packaging, content-protection, and live event broadcasting. Click [here](https://docs.microsoft.com/en-us/azure/media-services/) to know more.

* **Azure Video Indexer (AVI)**: Video Indexer consolidates various audio and video artificial intelligence (AI) technologies offered by Microsoft in one integrated service, making development simpler. Click [here](https://docs.microsoft.com/en-us/azure/media-services/video-indexer/video-indexer-use-apis) to know more. 

* **Azure Durable Functions (ADF)**: Durable Functions is an extension of Azure Functions that lets you write stateful functions in a serverless compute environment. Click [here](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview) to know more.

* **Azure Logic Apps (ALA)**: Azure Logic Apps is a cloud service that helps you schedule, automate, and orchestrate tasks, business processes, and workflows when you need to integrate apps, data, systems, and services across enterprises or organizations. Click [here](https://docs.microsoft.com/en-us/azure/logic-apps/logic-apps-overview) to know more.

## Architectural view

The proposed architecture can be seen below. In summary, this is what happens when a new video arrives into a specific container (here suggested as "incoming-videos") within Azure an given Azure Storage Account:

1. A new event is triggered by the storage account and is captured by a Logic App.

2. Logic App then capture the information previously sent and calls out a new function that validates the information received and then, starts a new stateful video processing flow by calling a orchestration function under ADF.

3. Under-the-hood ADF does call action functions whereby both the ingestion, encoding, publishing and analytics routines are performed. All these action functions actually do is to call for specific routines provided by AMS and wait for its responses.

![Solution architectural view](https://raw.githubusercontent.com/AzureForEducation/demo-videoprocessing/master/images/Video-Kroton-Arch.png)

## How to run it

Considering you already have both [.NET 2.2 (+)](https://dotnet.microsoft.com/download) and the lastest version of [Azure SDK](https://azure.microsoft.com/en-us/downloads/) installed in your computer, all you have to do towards to get it operational, is described below.

> This ain't a requirement but you can make this process considerable easier if you take advantage of [Visual Studio 2017 (+)](https://visualstudio.microsoft.com/) tooling.

### Step 1: Storage Account creation

Each of these services in Azure relies on storage accounts so the first step here would be going after the process of creating it. Please, follow [this link](https://docs.microsoft.com/en-us/azure/storage/common/storage-quickstart-create-account?tabs=azure-portal) to see a tutorial on how to get there.

After have it created, add a new private container called "incoming-videos".

### Step 2: Azure Media Services (AMS)

Create a new Azure Media Services Account (there is a very nice tutorial [in here](https://docs.microsoft.com/en-us/azure/media-services/previous/media-services-portal-create-account) on how to do it). Don't forget to tie up the storage account just created with this AMS account.

After its creation, don't forget to enable the "Streaming Endpoint" for the account or you wouldn't be able to see the actual result of the flow: videos being played.

### Step 3: Starter Logic App

As mentioned before, the solution utilizes a logic app to actually listen the events occurring within the storage account and react to those. So, you need to go after it. [In here](https://docs.microsoft.com/en-us/azure/logic-apps/quickstart-create-first-logic-app-workflow) you will be able to see a very nice tutorial taughing how to do it.

At the end, your Logic App flow should look like that one presented by the Figure below.

![Logic App Starter general view](https://raw.githubusercontent.com/AzureForEducation/demo-videoprocessing/master/images/logicapp-starter-view.PNG)

Where:

1. The first action defines both a connection with the storage account and determines the container which events will be listen from. Please, see the Figure below.

![Setting up the communication with storage account](https://raw.githubusercontent.com/AzureForEducation/demo-videoprocessing/master/images/logicapp-starter-block1.PNG)

2. The second action calls a regular Azure Function passing some dynamic information on the body, as you can see through the image below.

![Calling the starter Function](https://raw.githubusercontent.com/AzureForEducation/demo-videoprocessing/master/images/logicapp-starter-block2.PNG)

3. At the end, the Logic App then collects the results retuned by the orchestration function, and finally, drops an email to the configured recipient notifying it about the conclusion of the process.

![Configuring email dispatch](https://raw.githubusercontent.com/AzureForEducation/demo-videoprocessing/master/images/logicapp-starter-block3.PNG)

### Step 4: Publish the function code into Azure

Actually, there are several ways by which you could publish this code out into Azure like, setting up continous integration over Azure DevOps, FTP, so on and so so forth. For test purposes tough, the easiest way to get there is to web deploy protocol over Visual Studio. This is what I'm actually doing to get it done here.

> NOTE: Because the entire processing flow can take several minutes to complete and also, considering that Functions running under Consumption Plan are limited to 10 minutes max before it returns time-out, I strongly recommend you deploy it into a Function running under a regular hosting plan. [In here](https://docs.microsoft.com/en-us/azure/azure-functions/functions-scale) you can learn more about this constraint.

At the end, you should have a Function App pretty similar to that one presented by the Figure below sitting on top of your Azure environment.

![Function published](https://raw.githubusercontent.com/AzureForEducation/demo-videoprocessing/master/images/function-publish.PNG)

### Step 5: Connecting your Azure Media Services account to your Video Index instance

At this point, Azure Media Services and the new version of Video Indexer are two separate services that can work together. It means that you can use Video Indexer features within your existing AMS account to get the job done.

Because we're interested on having the insights extraction as result of the video processing flow, I'm going to bring it together and for this, I need to connect my Video Indexer account with my AMS account. The procedure to get it done is well detailed [in here](https://docs.microsoft.com/en-us/azure/media-services/video-indexer/connect-to-azure).
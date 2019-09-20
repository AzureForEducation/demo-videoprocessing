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

### Step 3: Logic App creation

As mentioned before, the solution utilizes a logic app to actually listen the events occurring within the storage account and react to those. So, you need to go after it. [In here](https://docs.microsoft.com/en-us/azure/logic-apps/quickstart-create-first-logic-app-workflow) you will be able to see a very nice tutorial taughing how to do it.

At the end, your Logic App flow should look like that one presented by the Figure below.

![Logic App Starter general view](https://raw.githubusercontent.com/AzureForEducation/demo-videoprocessing/master/images/logicapp-starter-view.PNG)

Where:

* The first action defines both a connection with the storage account and determines the container which events will be listen from.


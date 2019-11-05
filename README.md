# learn.forge.designautomation

## Description
This is a sample showing a simple configuration app built on Forge. It is based on the [Learn Forge](http://learnforge.autodesk.io) tutorials. 

The following workflows are demostrated in this sample app:

* Extract the **User Parameters** From an Inventor Data Set 
* Change paramters and update the model
* Extract the **BOM** from the updated model
* Create a **Drawing** from the updated model
* Download the updated data set and BOM

## Pre-reqs
Make sure you have the following installed

* Visual Studio 2017 or 2019
* IIS Express
* ngrok

You will also need to setup a Forge account at forge.autodesk.com

This application uses the following AppBundles. You'll need to build and deploy these AppBundle and Activities:

* https://github.com/akenson/da-extract-params
* https://github.com/akenson/da-update-user-params
* https://github.com/akenson/da-update-bom
* https://github.com/akenson/da-create-drawing
* https://github.com/akenson/da-sat-output
* https://github.com/akenson/da-rfa-output

See the Readme.md in each of the repositories above for instructions

### ngrok
To run ngrok on Windows you'll need to start it with the following command:
```
ngrok http 3000 -host-header="localhost:3000"
```

## Setup
1. Open the designautomation.sln file in Visual Studio
2. Edit the Properties of the `forgesample` solution and add the following `Environment Variables` in the Debug section

| Key | Value |
|-----|-------|
|ASPNETCORE_URLS|http://localhost:3000|
|FORGE_CLIENT_SECRET|_your forge app client secret_|
|FORGE_CLIENT_ID|_your forge app client id_|
|ASPNETCORE_ENVIRONMENT|Development|
|FORGE_WEBHOOK_URL|_your ngrok url_|

3. a) Upload a zip file containing your Inventor Assembly and Project file to a Forge OSS bucket that is in the form of `<your app id lower case>_designautomation`
b) Upload Revit Template file to Forge OSS bucked named <your app id lower case>_designautomation/object/RevitTemplate
(generic model template is located in AppBundle "da-rfa-output/RevitTemplate/MetricGenericModel.rft")

_insert Postman screenshot here??_
_provide default data set??_

4. In Visual Studio make sure the `forgesample` is the startup project and start debugging. This should launch a web browser with `localhost:3000`.




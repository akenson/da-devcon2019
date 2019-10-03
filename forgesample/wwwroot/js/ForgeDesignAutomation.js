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

$(document).ready(function () {
    prepareLists();

    $('#clearAccount').click(clearAccount);
    $('#defineActivityShow').click(defineActivityModal);
    $('#createAppBundleActivity').click(createAppBundleActivity);
    $('#startExtractParams').click(startExtractParams);
    $('#startUpdateModel').click(startUpdateModel);

    startConnection();

    // !AA! For testing local viewer, having trouble with this in IIS
    //launchViewer("viewables/viewable/bubble.json");
});

function prepareLists() {
    //list('activity', '/api/forge/designautomation/activities');
    //list('engines', '/api/forge/designautomation/engines');
    //list('localBundles', '/api/appbundles');
    list('inputFile', '/api/forge/datamanagement/objects');
}

function list(control, endpoint) {
    $('#' + control).find('option').remove().end();
    jQuery.ajax({
        url: endpoint,
        success: function (list) {
            if (list.length === 0)
                $('#' + control).append($('<option>', { disabled: true, text: 'Nothing found' }));
            else
                list.forEach(function (item) { $('#' + control).append($('<option>', { value: item, text: item })); })
        }
    });
}

function clearAccount() {
    if (!confirm('Clear existing activities & appbundles before start. ' +
        'This is useful if you believe there are wrong settings on your account.' +
        '\n\nYou cannot undo this operation. Proceed?')) return;

    jQuery.ajax({
        url: 'api/forge/designautomation/account',
        method: 'DELETE',
        success: function () {
            prepareLists();
            writeLog('Account cleared, all appbundles & activities deleted');
        }
    });
}

function defineActivityModal() {
    $("#defineActivityModal").modal();
}

function createAppBundleActivity() {
    startConnection(function () {
        writeLog("Defining appbundle and activity for " + $('#engines').val());
        $("#defineActivityModal").modal('toggle');
        createAppBundle(function () {
            createActivity(function () {
                prepareLists();
            })
        });
    });
}

function createAppBundle(cb) {
    jQuery.ajax({
        url: 'api/forge/designautomation/appbundles',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            zipFileName: $('#localBundles').val(),
            engine: $('#engines').val()
        }),
        success: function (res) {
            writeLog('AppBundle: ' + res.appBundle + ', v' + res.version);
            if (cb) cb();
        }
    });
}

function createActivity(cb) {
    jQuery.ajax({
        url: 'api/forge/designautomation/activities',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            zipFileName: $('#localBundles').val(),
            engine: $('#engines').val()
        }),
        success: function (res) {
            writeLog('Activity: ' + res.activity);
            if (cb) cb();
        }
    });
}

function startExtractParams() {

    startConnection(function () {
        var data = JSON.stringify({
            documentPath: $('#documentPath').val(),
            projectPath: $('#projectPath').val(),
            inputFile: $('#inputFile').val(),
            browerConnectionId: connectionId
        });
        writeLog('Getting document parameters ...');
        $.ajax({
            url: 'api/forge/designautomation/workitems/extractparams',
            data: data,
            contentType: 'application/json',
            //processData: false,
            //contentType: false,
            method: 'POST',
            success: function (res) {
                writeLog('Workitem started: ' + res.workItemId);
            }
        });
    });
}

function startUpdateModel() {

    startConnection(function () {
        var file = $('#inputFile').val();
        var updateData = {
            'file': file,
            browerConnectionId: connectionId,
            parameters: {}
        };

        var children = document.getElementById("parameters").childNodes;
        for (var i = 0; i < children.length; i++) {
            var item = children[i].children[1];
            var id = item.id.split('parameters_').pop();
            var value = item.value;
            updateData.parameters[id] = value;
            console.log("id: " + id + ", value: " + value);
        }

        var updateDataStr = JSON.stringify(updateData);

        writeLog('Updating model with new params...');
        $.ajax({
            url: 'api/forge/designautomation/workitems/updatemodel',
            data: updateDataStr,
            contentType: 'application/json',
            method: 'POST',
            success: function (res) {
                writeLog('Workitem started: ' + res.workItemId);
            }
        });
    });
}

function writeLog(text) {
    $('#outputlog').append('<div style="border-top: 1px dashed #C0C0C0">' + text + '</div>');
    var elem = document.getElementById('outputlog');
    elem.scrollTop = elem.scrollHeight;
}

function updateParameters(message) {
    var parameters = $('#parameters');
    parameters.html('');

    let json = JSON.parse(message);
    for (let key in json) {
        let item = json[key];
        let id = `parameters_${key}`;

        if (item.values && item.values.length > 0) {
            parameters.append($(`
        <div class="form-group">
          <label for="${id}">${key}</label>
          <select class="form-control" id="${id}"></select>
        </div>`));
            let select = $(`#${id}`);
            for (let key2 in item.values) {
                let value = item.values[key2];
                select.append($('<option>', { value: value, text: value }))
            }
            // Activate current selection
            select.val(item.value);
        } else if (item.unit === "Boolean") {
            parameters.append($(`
        <div class="form-group">
          <label for="${id}">${key}</label>
          <select class="form-control" id="${id}">
            <option value="True">True</option>
            <option value="False">False</option>
          </select>
        </div>`));
            let select = $(`#${id}`);
            select.val(item.value);
        } else {
            parameters.append($(`
        <div class="form-group">
          <label for="${id}">${key}</label>
          <input type="text" class="form-control" id="${id}" placeholder="Enter new ${key} value">
        </div>`));
            let input = $(`#${id}`);
            input.val(item.value);
        }
    }
}

function updateViewable(message) {

}

var connection;
var connectionId;

function startConnection(onReady) {
    if (connection && connection.connectionState) { if (onReady) onReady(); return; }
    connection = new signalR.HubConnectionBuilder().withUrl("/api/signalr/designautomation").build();
    connection.start()
        .then(function () {
            connection.invoke('getConnectionId')
                .then(function (id) {
                    connectionId = id; // we'll need this...
                    if (onReady) onReady();
                });
        });

    connection.on("downloadResult", function (url) {
        writeLog('<a href="' + url + '">Download result file here</a>');
    });

    connection.on("onComplete", function (message) {
        writeLog(message);
    });

    connection.on("onParameters", function (message) {
        updateParameters(message);
    });

    connection.on("onViewableUpdate", function (message) {
        // !AA! Fix this once I figure out how to load local SVF with IIS
        //updateViewable(message);
        //launchViewer("viewables/viewable/bubble.json");
    });
}

// Get public access token for read only,
// using ajax to access route /api/forge/oauth/public in the background
function getForgeToken(callback) {
    console.log('Getting token...');
    jQuery.ajax({
        url: '/api/forge/oauth/public',
        success: function (res) {
            callback(res.access_token, res.expires_in);
        }
    });
}

var viewerApp;

function launchViewer(url) {
    var options = {
        env: 'AutodeskProduction',
        api: 'derivativeV2',
        getAccessToken: getForgeToken,
    };

    Autodesk.Viewing.Initializer(options, () => {
        viewerApp = new Autodesk.Viewing.ViewingApplication('MyViewerDiv');
        //viewer = new Autodesk.Viewing.GuiViewer3D(document.getElementById('MyViewerDiv'))
        viewerApp.registerViewer(viewerApp.k3D, Autodesk.Viewing.Private.GuiViewer3D);
        viewerApp.loadDocument(url, onDocumentLoadSuccess, onDocumentLoadFailure);
        //viewer = new Autodesk.Viewing.GuiViewer3D(document.getElementById('forgeViewer'));
        //viewer.start();

    });
}

function onDocumentLoadSuccess(doc) {
    console.log('document load success');
    var viewables = viewerApp.bubble.search({ 'type': 'geometry' });
    if (viewables.length === 0) {
        console.error('Document contains no viewables.');
        return;
    }
    // Choose any of the avialble viewables
    viewerApp.selectItem(viewables[0].data, onItemLoadSuccess, onItemLoadFail);
}

function onDocumentLoadFailure(viewerErrorCode) {
    console.error('onDocumentLoadFailure() - errorCode:' + viewerErrorCode);
}

/**
     * viewer.selectItem() success callback.
     * Invoked after the model's SVF has been initially loaded.
     * It may trigger before any geometry has been downloaded and displayed on-screen.
     */
function onItemLoadSuccess(viewer, item) {
    console.log('onItemLoadSuccess()!');
    console.log(viewerApp);
    console.log(item);
    // Congratulations! The viewer is now ready to be used.
    console.log('Viewers are equal: ' + (viewerApp === viewerApp.getCurrentViewer()));
}
/**
* viewer.selectItem() failure callback.
* Invoked when there's an error fetching the SVF file.
*/
function onItemLoadFail(viewerErrorCode) {
    console.error('onLoadModelError() - errorCode:' + viewerErrorCode);
    jQuery('#MyViewerDiv').html('<p>There is an error fetching the translated SVF file. Please try refreshing the page.</p>');
}

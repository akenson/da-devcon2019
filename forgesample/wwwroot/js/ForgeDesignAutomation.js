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
    $('#startUpdateBOM').click(startUpdateBOM);
    $('#startUpdateDrawing').click(startUpdateDrawing);
    $('#clearLog').click(clearLog);

    $('#pills-tab a').on('click', function (e) {
        e.preventDefault()
        $(this).tab('show')
    })

    startConnection();

    // Uncomment to debug viewable
    //updateViewable('');

});

function prepareLists() {
    //list('activity', '/api/forge/designautomation/activities');
    //list('engines', '/api/forge/designautomation/engines');
    //list('localBundles', '/api/appbundles');
    list('inputFile', '/api/forge/datamanagement/objects');
}

function clearLog() {
    $('#outputlog').html('');
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
    clearLog();

    startConnection(function () {
        var file = $('#inputFile').val();
        var projectPath = $('#projectPath').val();
        var documentPath = $('#documentPath').val();
        var updateData = {
            'file': file,
            'projectPath': projectPath,
            'documentPath': documentPath,
            browerConnectionId: connectionId,
            parameters: {}
        };

        var children = document.getElementById("parameters").childNodes;
        for (var i = 1 /* first is a label */; i < children.length; i++) {
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

function startUpdateBOM() {
    clearLog();
    $('#bomTableBody').html('');

    startConnection(function () {
        var file = $('#inputFile').val(); 
        var projectPath = $('#projectPath').val();
        var documentPath = $('#documentPath').val();
        var updateData = {
            'file': file,
            'projectPath': projectPath,
            'documentPath': documentPath,
            browerConnectionId: connectionId
        };

        var updateDataStr = JSON.stringify(updateData);

        writeLog('Updating model with new params...');
        $.ajax({
            url: 'api/forge/designautomation/workitems/updatebom',
            data: updateDataStr,
            contentType: 'application/json',
            method: 'POST',
            success: function (res) {
                writeLog('Workitem started: ' + res.workItemId);
            }
        });
    });
}

function startUpdateDrawing() {
    clearLog();

    startConnection(function () {
        var file = $('#inputFile').val();
        var projectPath = $('#projectPath').val();
        var documentPath = $('#documentPath').val();
        var updateData = {
            'file': file,
            'projectPath': projectPath,
            'documentPath': documentPath,
            browerConnectionId: connectionId
        };

        var updateDataStr = JSON.stringify(updateData);

        writeLog('Updating model with new params...');
        $.ajax({
            url: 'api/forge/designautomation/workitems/updatedrawing',
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
    //parameters.html('');

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
        updateViewable(message);
    });

    connection.on("onBom", function (message) {
        updateBom(message);
    });

    connection.on("onDrawing", function (message) {
        updateDrawing(message);
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

function updateViewable(message) {
    writeLog(message);

    // Show the 3D Model Tab
    showTab('pills-3d-model');
    $('#pills-tab a[href="#pills-3d-model-tab"]').tab('show')

    // Launch the viewer with the result viewable
    launchViewer("viewables/viewable/bubble.json");
}

function updateBom(message) {
    //$('#bomTableBody').html('');
    var tableBody = $('#bomTableBody');
    let json = JSON.parse(message);
    for (let key in json) {
        let rowJson = json[key];
        console.log("row: " + rowJson);
        var rowNum = rowJson['row_number'];
        var partNum = rowJson['part_number'];
        var quantity = rowJson['quantity'];
        var descr = rowJson['description'];
        var material = rowJson['material'];

        tableBody.append($(`<tr>
        <th scope="row">${rowNum}</th>
        <td>${partNum}</td>
        <td>${quantity}</td>
        <td>${descr}</td>
        <td>${material}</td>
        </tr>`));
    }

    console.log(tableBody);
}

function updateDrawing(message) {
    $('#DrawingDiv').append('Drawing Updated!!');
}

var viewer;

function launchViewer(url) {
    var options = {
        env: 'AutodeskProduction',
        api: 'derivativeV2',
        getAccessToken: getForgeToken,
    };

    Autodesk.Viewing.Initializer(options, () => {
        var htmlDiv = document.getElementById('ModelDiv');
        viewer = new Autodesk.Viewing.GuiViewer3D(htmlDiv);
        var startedCode = viewer.start();
        Autodesk.Viewing.Document.load(url, onDocumentLoadSuccess, onDocumentLoadFailure);
    });
}

function onDocumentLoadSuccess(viewerDocument) {
    var defaultModel = viewerDocument.getRoot().getDefaultGeometry();
    viewer.loadDocumentNode(viewerDocument, defaultModel);
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
    jQuery('#ModelDiv').html('<p>There is an error fetching the translated SVF file. Please try refreshing the page.</p>');
}

function showTab(tab) {
    $('.nav-tabs a[href="#' + tab + '"]').tab('show');
};
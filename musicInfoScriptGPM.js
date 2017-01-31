var oldData;
var ws;
var connected = false;
var reconnect;

function open()
{
    try {
        var url = "ws://127.0.0.1:8974/";
        ws = new WebSocket(url);
        ws.onopen = onOpen;
        ws.onclose = onClose;
        ws.onmessage = onMessage;
        ws.onerror = onError;

        console.log("Opening websocket");
    }
    catch (error)
    {
        console.log("Error:" + error);
    }
}

var onOpen = function() {
    console.log("Opened websocket");
    connected = true;
    clearTimeout(reconnect);
    dataCheck();
};

var onClose = function() {
    console.log("Closed websocket");
    connected = false;
    reconnect = setTimeout(function(){ open(); }, 1000);
};

var onMessage = function(event) {
    console.log("Message received:" + event.data);
};

var onError = function(event) {
    if(typeof event.data != 'undefined') {
        console.log("Websocket Error:" + event.data);
    }
};

function dataCheck()
{
    try
    {
        if(document.getElementsByClassName("song-row currently-playing")[0].innerHTML != oldData)
        {
            oldData = document.getElementsByClassName("song-row currently-playing")[0].innerHTML;

            if(connected) {
                ws.send(oldData);
            }
        }
        setTimeout(dataCheck, 500);
    }
    catch(err)
    {
        setTimeout(dataCheck, 1000);
    }
}

open();
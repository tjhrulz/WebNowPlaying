var oldData;
var ws;
var connected = false;

function open()
{
    var url = "ws://127.0.0.1:8974/";
    ws = new WebSocket(url);
    ws.onopen = onOpen;
    ws.onclose = onClose;
    ws.onmessage = onMessage;
    ws.onerror = onError;

    console.log("Opening websocket");
}

var onOpen = function() {
    console.log("Opened websocket");
    connected = true;
    dataCheck();
};

var onClose = function() {
    console.log("Closed websocket");
    connected = false;
};

var onMessage = function(event) {
    console.log("Message received" + event.data);
};

var onError = function(event) {
    alert(event.data);
};

function dataCheck()
{
    try
    {
        if(document.getElementsByClassName("song-row currently-playing")[0].innerHTML != oldData)
        {
          oldData = document.getElementsByClassName("song-row currently-playing")[0].innerHTML;

          ws.send(oldData);
        }
        setTimeout(dataCheck, 5000);
    }
    catch(err)
    {
        setTimeout(dataCheck, 10000);
    }
}

open();
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
    reconnect = setTimeout(function(){ open(); }, 5000);
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
        //Contains both the title and the album art

        newData = document.getElementsByClassName("song-row currently-playing")[0].innerHTML
        if(newData != oldData)
        {
            oldData = newData;

            title = document.getElementById("currently-playing-title").title;
            artist = document.getElementById("player-artist").innerHTML;
            album = document.getElementsByClassName("player-album")[0].innerHTML;
            //Only contains album art thumbnail not full sized
            albumArt =  document.getElementById("playerBarArt").src;

            position = document.getElementById("time_container_current").innerHTML;
            duration =  document.getElementById("time_container_duration").innerHTML;

            liked = document.getElementsByClassName("rating-container materialThumbs")[0].children[0].title;
            disliked = document.getElementsByClassName("rating-container materialThumbs")[0].children[1].title;

            //Note this may get info right when using podcasts
            //.children[0 & 6].title is back 30 seconds and forward 30 seconds
            //.children[2 & 4].title are previous and next
            repeat = document.getElementsByClassName("material-player-middle")[0].children[1].title;
            shuffle = document.getElementsByClassName("material-player-middle")[0].children[5].title;
            status = document.getElementsByClassName("material-player-middle")[0].children[3].title;

            if(connected) {
                ws.send(title);
                ws.send(artist);
                ws.send(album);
                ws.send(status);

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
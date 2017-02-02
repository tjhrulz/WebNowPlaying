var oldTitle;
var oldPos;
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

        newPos = document.getElementsByClassName("playbackTimeline__timePassed")[0].children[1].innerHTML;
        newTitle = document.getElementsByClassName("playbackSoundBadge__title sc-truncate")[0].title;

        if(newTitle != oldTitle || newPos != oldPos)
        {
            oldTitle = newTitle;
            oldPos = newPos;

            title = newTitle;
            artist = document.getElementsByClassName("playbackSoundBadge__context sc-link-light sc-truncate")[0].title;
            //album = document.getElementsByClassName("player-album")[0].innerHTML;
            //Only contains album art thumbnail not full sized
            albumArt =  document.getElementsByClassName("playbackSoundBadge")[0].children[0].children[0].children[0];

            position = newPos;
            duration = document.getElementsByClassName("playbackTimeline__duration")[0].children[1].innerHTML;

            liked = document.getElementsByClassName("sc-button-like playbackSoundBadge__like sc-button sc-button-small sc-button-responsive sc-button-icon")[0].title;
            disliked = 0;

            //Note this may get info right when using podcasts
            //.children[0 & 6].title is back 30 seconds and forward 30 seconds
            //.children[2 & 4].title are previous and next
            repeat = 0;
            shuffle = 0;
            if(document.getElementsByClassName("repeatControl sc-ir m-old m-one").length > 0)
            {
                repeat = 1
            }

            status = document.getElementsByClassName("playControl sc-ir playControls__icon playControls__play")[0].title;

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
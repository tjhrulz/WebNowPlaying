using System;
using System.Runtime.InteropServices;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using Rainmeter;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WebNowPlaying
{
    //@TODO Write plugin to use multiple services and accept custom port configurations

    internal class Measure
    {
        public class MusicInfo
        {
            public MusicInfo()
            {
                Player = "";
                Title = "";
                Artist = "";
                Album = "";
                AlbumArt = "";
                Duration = "";
                Position = "";
                State = 0;
                Rating = 0;
                Repeat = 0;
                Shuffle = 0;

            }

            public string Player { get; set; }
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Album { get; set; }
            public string AlbumArt { get; set; }
            public string Duration { get; set; }
            public string Position { get; set; }
            public int State { get; set; }
            public int Rating { get; set; }
            public int Repeat { get; set; }
            public int Shuffle { get; set; }
        }

        enum InfoTypes
        {
            Status,
            Player,
            Title,
            Artist,
            Album,
            AlbumArt,
            Duration,
            Position,
            State,
            Rating,
            Repeat,
            Shuffle
        }

        public static WebSocketServer wssv;

        //Dictionary of music info, key is websocket client id
        public static Dictionary<string, MusicInfo> musicInfo = new Dictionary<string, MusicInfo>();
        //List of websocket client ids in order of update of client (Last location is most recent)
        private static List<string> lastUpdatedID = new List<string>();

        private InfoTypes playerType = InfoTypes.Status;

        public class WebNowPlaying : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                string type = e.Data.Substring(0, e.Data.IndexOf(":"));
                string info = e.Data.Substring(e.Data.IndexOf(":") + 1);


                //If already in list remove it
                lastUpdatedID.Remove(this.ID);
                lastUpdatedID.Add(this.ID);

                MusicInfo currMusicInfo = new MusicInfo();
                if(!musicInfo.TryGetValue(this.ID, out currMusicInfo))
                {
                    //This should never happen but just in case do this to prevent getting an error
                    musicInfo.Add(this.ID, currMusicInfo);
                    musicInfo.TryGetValue(this.ID, out currMusicInfo);
                }


                if (type.ToUpper() == InfoTypes.Player.ToString().ToUpper())
                {
                    currMusicInfo.Player = info;
                }
                else if (type.ToUpper() == InfoTypes.Title.ToString().ToUpper())
                {
                    currMusicInfo.Title = info;
                }
                else if (type.ToUpper() == InfoTypes.Artist.ToString().ToUpper())
                {
                    currMusicInfo.Artist = info;
                }
                else if (type.ToUpper() == InfoTypes.Album.ToString().ToUpper())
                {
                    currMusicInfo.Album = info;
                }
                else if (type.ToUpper() == InfoTypes.AlbumArt.ToString().ToUpper())
                {
                    currMusicInfo.AlbumArt = info;
                }
                else if (type.ToUpper() == InfoTypes.Duration.ToString().ToUpper())
                {
                    currMusicInfo.Duration = info;
                }
                else if (type.ToUpper() == InfoTypes.Position.ToString().ToUpper())
                {
                    currMusicInfo.Position = info;
                }
                else if (type.ToUpper() == InfoTypes.State.ToString().ToUpper())
                {
                    try
                    {
                        currMusicInfo.State = Convert.ToInt16(info);
                    }
                    catch
                    {
                        API.Log(API.LogType.Error, "Error converting state to integer, state was:" + info);
                    }
                }
                else if (type.ToUpper() == InfoTypes.Rating.ToString().ToUpper())
                {
                    try
                    {
                        currMusicInfo.Rating = Convert.ToInt16(info);
                    }
                    catch
                    {
                        API.Log(API.LogType.Error, "Error converting rating to integer, rating was:" + info);
                    }
                }
                else if (type.ToUpper() == InfoTypes.Repeat.ToString().ToUpper())
                {
                    try
                    {
                        currMusicInfo.Repeat = Convert.ToInt16(info);
                    }
                    catch
                    {
                        API.Log(API.LogType.Error, "Error converting repeat state to integer, repeat state was:" + info);
                    }
                }
                else if (type.ToUpper() == InfoTypes.Shuffle.ToString().ToUpper())
                {
                    try
                    {
                        currMusicInfo.Shuffle = Convert.ToInt16(info);
                    }
                    catch
                    {
                        API.Log(API.LogType.Error, "Error converting shuffle state to integer, shuffle state was:" + info);
                    }
                }
            }

            protected override void OnOpen()
            {
                base.OnOpen();

                MusicInfo currMusicInfo = new MusicInfo();
                musicInfo.Add(this.ID, currMusicInfo);
            }
            protected override void OnClose(CloseEventArgs e)
            {
                base.OnClose(e);

                if (musicInfo.Remove(this.ID))
                {
                    //While it should always be safe to assume lastUpdateID has contents if we did remove content from musicInfo we can be more certain
                    lastUpdatedID.RemoveAt(lastUpdatedID.Count - 1);
                }

            }
            public void SendMessage(string stringToSend)
            {
                Sessions.Broadcast(stringToSend);
            }
        }



        internal Measure(Rainmeter.API api)
        {
            if (wssv == null)
            {
                //@TODO Declare on reload so that custom ports can be allowed
                wssv = new WebSocketServer(8974);
                wssv.AddWebSocketService<WebNowPlaying>("/");
            }

            if (wssv.IsListening == false)
            {
                wssv.Start();
            }
        }

        internal virtual void Dispose()
        {

        }

        internal virtual void Reload(Rainmeter.API api, ref double maxValue)
        {
            //@TODO Use this port
            int port = api.ReadInt("Port", 58932);

            string playerTypeString = api.ReadString("PlayerType", "Status");
            try
            {
                playerType = (InfoTypes)Enum.Parse(typeof(InfoTypes), playerTypeString, true);
            }
            catch
            {
                API.Log(API.LogType.Error, "WebNowPlaying.dll - Unknown PlayerType:" + playerTypeString);
                playerType = InfoTypes.Status;
            }
        }

        internal void ExecuteBang(string args)
        {
            string bang = args.ToLowerInvariant();

            //@TODO Implement keeping of more than just the last update song
            WebSocketServiceHost host;

            if (bang.Equals("playpause"))
            {
                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                host.Sessions.SendTo("PlayPause", lastUpdatedID[lastUpdatedID.Count - 1]);
            }
            else if (bang.Equals("next"))
            {
                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                host.Sessions.SendTo("next", lastUpdatedID[lastUpdatedID.Count - 1]);
            }
            else if (bang.Equals("previous"))
            {
                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                host.Sessions.SendTo("previous", lastUpdatedID[lastUpdatedID.Count - 1]);
            }
            else if (bang.Equals("repeat"))
            {
                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                host.Sessions.SendTo("repeat", lastUpdatedID[lastUpdatedID.Count - 1]);
            }
            else if (bang.Equals("shuffle"))
            {
                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                host.Sessions.SendTo("shuffle", lastUpdatedID[lastUpdatedID.Count - 1]);
            }
        }

        internal virtual double Update()
        {
            MusicInfo currMusicInfo = new MusicInfo();
            bool found = false;
            //Only try to get new info if there could be some in it
            if (lastUpdatedID.Count > 0)
            {
                found = musicInfo.TryGetValue(lastUpdatedID[lastUpdatedID.Count - 1], out currMusicInfo);
            }

            //If tried to find and not found
            if (!found && lastUpdatedID.Count > 0)
            {
                API.Log(API.LogType.Error, "WebNowPlaing.dll - Music info not found with that id");
                currMusicInfo = new MusicInfo();
            }

            switch (playerType)
            {
                case InfoTypes.State:
                    return currMusicInfo.State;
                case InfoTypes.Status:
                    //@TODO Implment this to be 1 if any connected 0 if none connected
                    return wssv.WebSocketServices.SessionCount;
                case InfoTypes.Rating:
                    return currMusicInfo.Rating;
                case InfoTypes.Repeat:
                    return currMusicInfo.Repeat;
                case InfoTypes.Shuffle:
                    return currMusicInfo.Shuffle;
            }

            return 0.0;
        }

        internal string GetString()
        {
            MusicInfo currMusicInfo = new MusicInfo();
            bool found = false;
            //Only try to get new info if there could be some in it
            if (lastUpdatedID.Count > 0)
            {
                found = musicInfo.TryGetValue(lastUpdatedID[lastUpdatedID.Count - 1], out currMusicInfo);
            }

            //If tried to find and not found
            if (!found && lastUpdatedID.Count > 0)
            {
                API.Log(API.LogType.Error, "WebNowPlaing.dll - Music info not found with that id");
                currMusicInfo = new MusicInfo();
            }

            switch (playerType)
            {
                case InfoTypes.Player:
                    return currMusicInfo.Player;
                case InfoTypes.Title:
                    return currMusicInfo.Title;
                case InfoTypes.Artist:
                    return currMusicInfo.Artist;
                case InfoTypes.Album:
                    return currMusicInfo.Album;
                case InfoTypes.AlbumArt:
                    return currMusicInfo.AlbumArt;
                case InfoTypes.Position:
                    return currMusicInfo.Position;
                case InfoTypes.Duration:
                    return currMusicInfo.Duration;
            }

            return null;
        }


    }
    public static class Plugin
    {
        static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure(new Rainmeter.API(rm))));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            //Now just keeps the websocket server open to limit reconnects
            GCHandle.FromIntPtr(data).Free();

            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }

        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = measure.GetString();
            if (stringValue != null)
            {
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);
            }

            return StringBuffer;
        }
        [DllExport]
        public static void ExecuteBang(IntPtr data, IntPtr args)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.ExecuteBang(Marshal.PtrToStringUni(args));
        }
    }
}

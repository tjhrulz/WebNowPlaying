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
        public static int instanceCount = 0;
        public static MusicInfo musicInfo = new MusicInfo();
        private InfoTypes playerType = InfoTypes.Status;
        private static string lastUpdatedID = "";

        public class WebNowPlaying : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                string type = e.Data.Substring(0, e.Data.IndexOf(":"));
                string info = e.Data.Substring(e.Data.IndexOf(":") + 1);

                lastUpdatedID = this.ID;

                if (type.ToUpper() == InfoTypes.Player.ToString().ToUpper())
                {
                    musicInfo.Player = info;
                }
                else if (type.ToUpper() == InfoTypes.Title.ToString().ToUpper())
                {
                    musicInfo.Title = info;
                }
                else if (type.ToUpper() == InfoTypes.Artist.ToString().ToUpper())
                {
                    musicInfo.Artist = info;
                }
                else if (type.ToUpper() == InfoTypes.Album.ToString().ToUpper())
                {
                    musicInfo.Album = info;
                }
                else if (type.ToUpper() == InfoTypes.AlbumArt.ToString().ToUpper())
                {
                    musicInfo.AlbumArt = info;
                }
                else if (type.ToUpper() == InfoTypes.Duration.ToString().ToUpper())
                {
                    musicInfo.Duration = info;
                }
                else if (type.ToUpper() == InfoTypes.Position.ToString().ToUpper())
                {
                    musicInfo.Position = info;
                }
                else if (type.ToUpper() == InfoTypes.State.ToString().ToUpper())
                {
                    try
                    {
                        musicInfo.State = Convert.ToInt16(info);
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
                        musicInfo.Rating = Convert.ToInt16(info);
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
                        musicInfo.Repeat = Convert.ToInt16(info);
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
                        musicInfo.Shuffle = Convert.ToInt16(info);
                    }
                    catch
                    {
                        API.Log(API.LogType.Error, "Error converting shuffle state to integer, shuffle state was:" + info);
                    }
                }


                System.Diagnostics.Debug.WriteLine(e.Data);
                API.Log(API.LogType.Notice, e.Data);
            }

            protected override void OnOpen()
            {
                base.OnOpen();
            }
            protected override void OnClose(CloseEventArgs e)
            {
                base.OnClose(e);

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
                host.Sessions.SendTo("PlayPause", lastUpdatedID);
            }
            else if (bang.Equals("next"))
            {
                wssv.WebSocketServices.Broadcast("next");
            }
            else if (bang.Equals("previous"))
            {
                wssv.WebSocketServices.Broadcast("previous");
            }
            else if (bang.Equals("repeat"))
            {
                wssv.WebSocketServices.Broadcast("repeat");
            }
            else if (bang.Equals("shuffle"))
            {
                wssv.WebSocketServices.Broadcast("shuffle");
            }
        }

        internal virtual double Update()
        {
            switch(playerType)
            {
                case InfoTypes.State:
                    return musicInfo.State;
                case InfoTypes.Status:
                    //@TODO Implment this to be 1 if any connected 0 if none connected
                    return wssv.WebSocketServices.SessionCount;
                case InfoTypes.Rating:
                    return musicInfo.Rating;
                case InfoTypes.Repeat:
                    return musicInfo.Repeat;
                case InfoTypes.Shuffle:
                    return musicInfo.Shuffle;
            }

            return 0.0;
        }

        internal string GetString()
        {
            switch (playerType)
            {
                case InfoTypes.Title:
                    return musicInfo.Title;
                case InfoTypes.Artist:
                    return musicInfo.Artist;
                case InfoTypes.Album:
                    return musicInfo.Album;
                case InfoTypes.AlbumArt:
                    return musicInfo.AlbumArt;
                case InfoTypes.Position:
                    return musicInfo.Position;
                case InfoTypes.Duration:
                    return musicInfo.Duration;
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
            Measure.instanceCount++;
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {

            Measure.instanceCount--;
            if (Measure.instanceCount == 0)
            {
                Measure.wssv.Stop();
                Measure.wssv = null;
            }
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

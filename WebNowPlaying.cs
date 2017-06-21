using System;
using System.Runtime.InteropServices;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using Rainmeter;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Threading;

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
                Cover = "";
                CoverWebAddress = "";
                CoverByteArr = new byte[0];
                Duration = "0:00";
                DurationSec = 0;
                Position = "0:00";
                PositionSec = 0;
                Progress = 0.0;
                Volume = 100;
                State = 0;
                Rating = 0;
                Repeat = 0;
                Shuffle = 0;

            }

            public string Player { get; set; }
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Album { get; set; }
            public string Cover { get; set; }
            public string CoverWebAddress { get; set; }
            public byte[] CoverByteArr { get; set; }
            public string Duration { get; set; }
            public int DurationSec { get; set; }
            public string Position { get; set; }
            public int PositionSec { get; set; }
            public double Progress { get; set; }
            public int Volume { get; set; }
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
            Cover,
            CoverWebAddress,
            Duration,
            Position,
            Progress,
            Volume,
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

        //Fallback location to download coverart to
        private static string CoverOutputLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Rainmeter/WebNowPlaying/cover.png";
        private string CoverDefaultLocation = "";

        //If true write through to disk right away
        private static volatile bool writeThrough = false;

        private InfoTypes playerType = InfoTypes.Status;

        public class WebNowPlaying : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs arg)
            {
                string type = arg.Data.Substring(0, arg.Data.IndexOf(":"));
                string info = arg.Data.Substring(arg.Data.IndexOf(":") + 1);


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
                else
                {

                    if (type.ToUpper() == InfoTypes.Title.ToString().ToUpper())
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
                    else if (type.ToUpper() == InfoTypes.Cover.ToString().ToUpper())
                    {
                        currMusicInfo.Cover = null;

                        if(lastUpdatedID.Count > 0 && lastUpdatedID[lastUpdatedID.Count - 1] == this.ID)
                        {
                            writeThrough = true;
                        }

                        Thread t = new Thread(() => GetImageFromUrl(this.ID, info, CoverOutputLocation));
                        t.Start();
                    }
                    else if (type.ToUpper() == InfoTypes.Duration.ToString().ToUpper())
                    {
                        //TODO Test this always comes before position, maybe set progress back to 0.0 in here
                        currMusicInfo.Duration = info;

                        try
                        {
                            string[] durArr = currMusicInfo.Duration.Split(':');

                            //Duration will always have seconds and minutes
                            int durSec = Convert.ToInt16(durArr[durArr.Length -1]);
                            int durMin = durArr.Length > 1 ? Convert.ToInt16(durArr[durArr.Length - 2]) * 60 : 0;
                            int durHour = durArr.Length > 2 ? Convert.ToInt16(durArr[durArr.Length - 3]) * 60 * 60 : 0;


                            currMusicInfo.DurationSec = durHour + durMin + durSec;
                            currMusicInfo.Progress = 0;
                        }
                        catch (Exception e)
                        {
                            API.Log(API.LogType.Error, "Error converting duration into integer");
                            API.Log(API.LogType.Debug, e.ToString());
                        }
                    }
                    else if (type.ToUpper() == InfoTypes.Position.ToString().ToUpper())
                    {
                        currMusicInfo.Position = info;

                        try
                        {
                            string[] posArr = currMusicInfo.Position.Split(':');

                            //Duration will always have seconds and minutes
                            int posSec = Convert.ToInt16(posArr[posArr.Length - 1]);
                            int posMin = posArr.Length > 1 ? Convert.ToInt16(posArr[posArr.Length - 2]) * 60 : 0;
                            int posHour = posArr.Length > 2 ? Convert.ToInt16(posArr[posArr.Length - 3]) * 60 * 60 : 0;


                            currMusicInfo.PositionSec = posHour + posMin + posSec;

                        }
                        catch (Exception e)
                        {
                            API.Log(API.LogType.Error, "Error converting position into integer");
                            API.Log(API.LogType.Debug, e.ToString());
                        }


                        if (currMusicInfo.DurationSec > 0)
                        {
                            currMusicInfo.Progress = (double)currMusicInfo.PositionSec / currMusicInfo.DurationSec * 100.0;
                        }
                        else
                        {
                            currMusicInfo.Progress = 100;
                        }

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
                    else if (type.ToUpper() == InfoTypes.Volume.ToString().ToUpper())
                    {
                        try
                        {
                            //For some odd reason toInt can not take a string containing a decimal directly so convert to decimal first
                            currMusicInfo.Volume = Convert.ToInt16(Convert.ToDecimal(info));
                        }
                        catch
                        {
                            API.Log(API.LogType.Error, "Error converting volume to integer, volume was:" + info);
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

                    if (currMusicInfo.State != 0)
                    {
                        //Get last index of lastUpdatedID so we can see if it changes
                        string lastID = "0";
                        if(lastUpdatedID.Count > 0)
                        {
                            lastID = lastUpdatedID[lastUpdatedID.Count - 1];
                        }

                        //Only add to last updated info other than player sent
                        //If already in list remove it
                        lastUpdatedID.Remove(this.ID);
                        lastUpdatedID.Add(this.ID);

                        //If last index has changed redownload album art
                        if(lastID != lastUpdatedID[lastUpdatedID.Count - 1])
                        {
                            MusicInfo lastUpdateMusicInfo;
                            if(musicInfo.TryGetValue(lastUpdatedID[lastUpdatedID.Count - 1], out lastUpdateMusicInfo))
                            {
                                if (lastUpdateMusicInfo.CoverByteArr.Length > 0)
                                {
                                    WriteStream(lastUpdatedID[lastUpdatedID.Count - 1], CoverOutputLocation, lastUpdateMusicInfo.CoverByteArr);
                                }
                                else
                                {
                                    writeThrough = true;
                                }
                            }

                        }
                    }
                    else
                    {
                        //Get last index of lastUpdatedID so we can see if it changes
                        string lastID = "0";
                        if (lastUpdatedID.Count > 0)
                        {
                            lastID = lastUpdatedID[lastUpdatedID.Count - 1];
                        }

                        //Remove it for list of ID's if the title has become blank
                        lastUpdatedID.Remove(this.ID);

                        //If last index has changed redownload album art
                        if (lastUpdatedID.Count > 0 && lastID != lastUpdatedID[lastUpdatedID.Count - 1])
                        {
                            MusicInfo lastUpdateMusicInfo;
                            if (musicInfo.TryGetValue(lastUpdatedID[lastUpdatedID.Count - 1], out lastUpdateMusicInfo))
                            {
                                if (lastUpdateMusicInfo.CoverByteArr.Length > 0)
                                {
                                    WriteStream(lastUpdatedID[lastUpdatedID.Count - 1], CoverOutputLocation, lastUpdateMusicInfo.CoverByteArr);
                                }
                                else
                                {
                                    writeThrough = true;
                                }
                            }

                        }
                        else if (lastUpdatedID.Count == 0)
                        {
                            lastUpdatedID.Add(this.ID);
                            MusicInfo lastUpdateMusicInfo;
                            if (musicInfo.TryGetValue(lastUpdatedID[lastUpdatedID.Count - 1], out lastUpdateMusicInfo))
                            {
                                if (lastUpdateMusicInfo.CoverByteArr.Length > 0)
                                {
                                    WriteStream(lastUpdatedID[lastUpdatedID.Count - 1], CoverOutputLocation, lastUpdateMusicInfo.CoverByteArr);
                                }
                                else
                                {
                                    writeThrough = true;
                                }
                            }
                        }
                    }
                }

                //System.Diagnostics.Debug.WriteLine(arg.Data);
                //API.Log(API.LogType.Notice, arg.Data);
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

                //If removing the last index in the update list and there is one before it download album art 
                if (lastUpdatedID.Count > 1)
                {
                    if(lastUpdatedID[lastUpdatedID.Count -1] == this.ID)
                    {
                        MusicInfo lastUpdateMusicInfo;
                        if (musicInfo.TryGetValue(lastUpdatedID[lastUpdatedID.Count - 2], out lastUpdateMusicInfo))
                        {
                            if (lastUpdateMusicInfo.CoverByteArr.Length > 0)
                            {
                                WriteStream(lastUpdatedID[lastUpdatedID.Count - 2], CoverOutputLocation, lastUpdateMusicInfo.CoverByteArr);
                            }
                            else
                            {
                                writeThrough = true;
                            }
                        }
                    }
                }

                lastUpdatedID.Remove(this.ID);
                musicInfo.Remove(this.ID);

            }
            public void SendMessage(string stringToSend)
            {
                Sessions.Broadcast(stringToSend);
            }
        }

        //For downloading the image, called in a thread in the onMessage for the websocket
        public static void GetImageFromUrl(string id, string url, string filePath)
        {
            try
            {
                // Create http request
                HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(url);
                using (HttpWebResponse httpWebReponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {

                    // Read as stream
                    using (Stream stream = httpWebReponse.GetResponseStream())
                    {
                        Byte[] image = ReadStream(stream);

                        MusicInfo currMusicInfo;
                        if (musicInfo.TryGetValue(id, out currMusicInfo))
                        {
                            currMusicInfo.CoverByteArr = image;
                            currMusicInfo.CoverWebAddress = url;

                            //If already flagged that an image is need write through to disk right away
                            if(writeThrough)
                            {
                                WriteStream(id, CoverOutputLocation, image);
                                writeThrough = false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "Unable to download album art to: " + CoverOutputLocation);
                Console.WriteLine(e);
            }
        }
        private static byte[] ReadStream(Stream input)
        {
            byte[] buffer = new byte[1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
        private static void WriteStream(string id, string filePath, Byte[] image)
        {
            if (CoverOutputLocation == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Rainmeter/WebNowPlaying/cover.png")
            {
                // Make sure the path folder exists if using it
                System.IO.Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Rainmeter/WebNowPlaying");
            }
            // Write stream to file
            File.WriteAllBytes(filePath, image);

            MusicInfo lastUpdateMusicInfo;
            if (musicInfo.TryGetValue(lastUpdatedID[lastUpdatedID.Count - 1], out lastUpdateMusicInfo))
            {
                lastUpdateMusicInfo.Cover = CoverOutputLocation;
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

                if(playerType == InfoTypes.Cover)
                {
                    //Unused @TODO Implement using this. Probably would be cleanest to null all other music info locations during write to disk
                    //defaultCoverLocation = api.ReadPath("DefaultPath", "");
                    string temp = api.ReadPath("CoverPath", null);
                    if (temp.Length > 0)
                    {
                        CoverOutputLocation = temp;
                    }
                    temp = api.ReadPath("DefaultPath", null);
                    if (temp.Length > 0)
                    {
                        CoverDefaultLocation = temp;
                    }
                }
                else if(playerType == InfoTypes.Progress)
                {
                    maxValue = 100;
                }
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

            if (lastUpdatedID.Count > 0)
            {
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
                else if (bang.Equals("togglethumbsup"))
                {
                    wssv.WebSocketServices.TryGetServiceHost("/", out host);
                    host.Sessions.SendTo("togglethumbsup", lastUpdatedID[lastUpdatedID.Count - 1]);
                }
                else if (bang.Equals("togglethumbsdown"))
                {
                    wssv.WebSocketServices.TryGetServiceHost("/", out host);
                    host.Sessions.SendTo("togglethumbsdown", lastUpdatedID[lastUpdatedID.Count - 1]);
                }
                else if (bang.Contains("rating"))
                {
                    wssv.WebSocketServices.TryGetServiceHost("/", out host);
                    if (bang.Equals("rating"))
                    {
                        host.Sessions.SendTo("rating", lastUpdatedID[lastUpdatedID.Count - 1]);
                    }
                    else
                    {
                        try
                        {

                            host.Sessions.SendTo("rating " + Convert.ToInt16(bang.Substring(bang.LastIndexOf(" ") + 1)), lastUpdatedID[lastUpdatedID.Count - 1]);
                        }
                        catch
                        {
                            API.Log(API.LogType.Error, "WebNowPlaing.dll - rating number not recognized assuming no rating");
                            host.Sessions.SendTo("rating 0", lastUpdatedID[lastUpdatedID.Count - 1]);
                        }
                    }
                }
                else if (bang.Contains("setposition"))
                {
                    API.Log(API.LogType.Error, "WebNowPlaing.dll - SetPosition not yet supported");
                }
                else if (bang.Contains("setvolume"))
                {
                    API.Log(API.LogType.Error, "WebNowPlaing.dll - SetVolume not yet supported");
                }
                else
                {
                    API.Log(API.LogType.Error, "WebNowPlaying.dll - Unknown bang:" + args);
                }
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
                    //@TODO have this possibly be per website
                    return wssv.WebSocketServices.SessionCount > 0 ? 1 : 0;
                case InfoTypes.Volume:
                    return currMusicInfo.Volume;
                case InfoTypes.Rating:
                    return currMusicInfo.Rating;
                case InfoTypes.Repeat:
                    return currMusicInfo.Repeat;
                case InfoTypes.Shuffle:
                    return currMusicInfo.Shuffle;
                case InfoTypes.Progress:
                    return currMusicInfo.Progress;
                case InfoTypes.Position:
                    return currMusicInfo.PositionSec;
                case InfoTypes.Duration:
                    return currMusicInfo.DurationSec;
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
                case InfoTypes.Cover:
                    if (currMusicInfo.Cover != null)
                    {
                        return currMusicInfo.Cover;
                    }
                    return CoverDefaultLocation;
                case InfoTypes.CoverWebAddress:
                    return currMusicInfo.CoverWebAddress;
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

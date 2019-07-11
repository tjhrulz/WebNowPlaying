using System;
using System.Runtime.InteropServices;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using Rainmeter;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Threading;

using System.Collections.Concurrent;

namespace WebNowPlaying
{
    //@TODO Write plugin to use multiple services and accept custom port configurations

    internal class Measure
    {
        public class MusicInfo
        {
            private string _Title;
            private int _State;

            public MusicInfo()
            {
                Player = "";
                _Title = "";
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
                Rating = 0;
                Repeat = 0;
                Shuffle = 0;
                _State = 0;
                TimeStamp = 0;
                TrackID = "";
                AlbumID = "";
                ArtistID = "";
                ID = "";

            }

            public string Player { get; set; }
            public string Title
            {
                get { return this._Title; }
                //This is important infomation to if this song should be displayed, update timestamp
                //Updating this on album and artist info is unneeded, just update for title
                set
                {
                    this._Title = value;

                    if (value != "")
                    {
                        this.TimeStamp = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                    }
                    else
                    {
                        //Just in case the title becomes null after it has been set reset it back to 0
                        this.TimeStamp = 0;
                    }
                }
            }
            public string Artist { get; set; }
            public string Album { get; set; }
            public string Cover { get; set; }
            public string CoverWebAddress { get; set; }
            public byte[] CoverByteArr { get; set; }
            public string Duration { get; set; }
            public int DurationSec { get; set; }
            public int DurationMs { get; set; }
            public string Position { get; set; }
            public int PositionSec { get; set; }
            public int PositionMs { get; set; }
            public double Progress { get; set; }
            public int Volume { get; set; }
            public int Rating { get; set; }
            public int Repeat { get; set; }
            public int Shuffle { get; set; }
            public string TrackID { get; set; }
            public string AlbumID { get; set; }
            public string ArtistID { get; set; }
            public int State
            {
                get { return this._State; }
                //This is important infomation to if this song should be displayed, update timestamp
                set
                {
                    this._State = value;
                    this.TimeStamp = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                }
            }
            public decimal TimeStamp { get; private set; }
            public string ID { get; set; }
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
            Shuffle,
            TrackID,
            AlbumID,
            ArtistID,
            PositionMs,
            DurationMs
        }

        public static WebSocketServer wssv;

        //Dictionary of music info, key is websocket client id
        public static ConcurrentDictionary<string, MusicInfo> musicInfo = new ConcurrentDictionary<string, MusicInfo>();
        public static MusicInfo displayedMusicInfo = new MusicInfo();

        private static Object spotifyTokenLock = new Object();

        //Fallback location to download coverart to
        private static string CoverOutputLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Rainmeter/WebNowPlaying/cover.png";
        private string CoverDefaultLocation = "";

        private static string rainmeterFileSettingsLocation = "";
        

        private InfoTypes playerType = InfoTypes.Status;

        public class WebNowPlaying : WebSocketBehavior
        {
            //Posibly lock this section?
            protected override void OnMessage(MessageEventArgs arg)
            {
                try
                {
                    string type = arg.Data.Substring(0, arg.Data.IndexOf(":"));
                    string info = arg.Data.Substring(arg.Data.IndexOf(":") + 1);


                    MusicInfo currMusicInfo = new MusicInfo();
                    if (!musicInfo.TryGetValue(this.ID, out currMusicInfo))
                    {
                        currMusicInfo = new MusicInfo();
                        //This should never happen but just in case do this to prevent getting an error
                        musicInfo.GetOrAdd(this.ID, currMusicInfo);
                    }

                    //I guess that TryGetValue can return an uninitialized value in some errors, so make sure it is good
                    if (currMusicInfo == null)
                    {
                        currMusicInfo = new MusicInfo();
                    }

                    currMusicInfo.ID = this.ID;
                    currMusicInfo.PositionMs = -1; // Set default value for PosMs so we can fallback to Pos if PosMs doesn't get set.

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
                            if (info.Length > 0)
                            {
                                Thread imageDownload = new Thread(() => GetImageFromUrl(this.ID, info));
                                imageDownload.Start();
                            }
                        }
                        else if (type.ToUpper() == InfoTypes.Duration.ToString().ToUpper())
                        {
                            //TODO Test this always comes before position, maybe set progress back to 0.0 in here
                            currMusicInfo.Duration = info;

                            try {
                                currMusicInfo.DurationSec = getMsFromTimeStamp(info) / 1000;
                                currMusicInfo.Progress = 0;
                            }
                            catch (Exception e)
                            {
                                API.Log(API.LogType.Error, $"WebNowPlaying.dll - Error converting duration into seconds ({info})");
                                API.Log(API.LogType.Debug, e.ToString());
                            }
                        } else if (type.ToUpper() == InfoTypes.DurationMs.ToString().ToUpper()) {
                            currMusicInfo.Duration = info;

                            try {
                                currMusicInfo.DurationMs = getMsFromTimeStamp(info);
                                currMusicInfo.DurationSec = getMsFromTimeStamp(info) / 1000;
                            } catch (Exception e) {
                                API.Log(API.LogType.Error, $"WebNowPlaying.dll - Error converting duration into milliseconds ({info})");
                                API.Log(API.LogType.Debug, e.ToString());
                            }
                        }
                        else if (type.ToUpper() == InfoTypes.Position.ToString().ToUpper())
                        {
                            currMusicInfo.Position = info;

                            try {
                                currMusicInfo.PositionSec = getMsFromTimeStamp(info) / 1000;
                            }
                            catch (Exception e)
                            {
                                API.Log(API.LogType.Error, $"WebNowPlaying.dll - Error converting position into seconds ({info})");
                                API.Log(API.LogType.Debug, e.ToString());
                            }

                            currMusicInfo.Progress = currMusicInfo.DurationSec > 0
                                ? (double) currMusicInfo.PositionSec / currMusicInfo.DurationSec * 100.0
                                : 100;
                        }
                        else if (type.ToUpper() == InfoTypes.PositionMs.ToString().ToUpper()) {
                            currMusicInfo.Position = info;

                            try {
                                currMusicInfo.PositionMs = getMsFromTimeStamp(info);
                                currMusicInfo.PositionSec = currMusicInfo.PositionMs / 1000;
                            } catch (Exception e) {
                                API.Log(API.LogType.Error, $"WebNowPlaying.dll - Error converting position into milliseconds ({info})");
                                API.Log(API.LogType.Debug, e.ToString());
                            }

                            currMusicInfo.Progress = currMusicInfo.DurationSec > 0
                                ? (double) currMusicInfo.PositionMs / (currMusicInfo.DurationSec * 1000) * 100
                                : 100;
                        }
                        else if (type.ToUpper() == InfoTypes.State.ToString().ToUpper())
                        {
                            try
                            {
                                currMusicInfo.State = Convert.ToInt16(info);
                            }
                            catch (Exception e)
                            {
                                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error converting state to integer, state was:" + info);
                                API.Log(API.LogType.Debug, e.ToString());
                            }
                        }
                        else if (type.ToUpper() == InfoTypes.Volume.ToString().ToUpper())
                        {
                            try
                            {
                                //For some odd reason toInt can not take a string containing a decimal directly so convert to decimal first
                                currMusicInfo.Volume = Convert.ToInt16(Convert.ToDecimal(info));
                            }
                            catch (Exception e)
                            {
                                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error converting volume to integer, volume was:" + info);
                                API.Log(API.LogType.Debug, e.ToString());
                            }
                        }
                        else if (type.ToUpper() == InfoTypes.Rating.ToString().ToUpper())
                        {
                            try
                            {
                                currMusicInfo.Rating = Convert.ToInt16(info);
                            }
                            catch (Exception e)
                            {
                                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error converting rating to integer, rating was:" + info);
                                API.Log(API.LogType.Debug, e.ToString());
                            }
                        }
                        else if (type.ToUpper() == InfoTypes.Repeat.ToString().ToUpper())
                        {
                            try
                            {
                                currMusicInfo.Repeat = Convert.ToInt16(info);
                            }
                            catch (Exception e)
                            {
                                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error converting repeat state to integer, repeat state was:" + info);
                                API.Log(API.LogType.Debug, e.ToString());
                            }
                        }
                        else if (type.ToUpper() == InfoTypes.Shuffle.ToString().ToUpper())
                        {
                            try
                            {
                                currMusicInfo.Shuffle = Convert.ToInt16(info);
                            }
                            catch (Exception e)
                            {
                                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error converting shuffle state to integer, shuffle state was:" + info);
                                API.Log(API.LogType.Debug, e.ToString());
                            }
                        }
                        else if (type.ToUpper() == InfoTypes.TrackID.ToString().ToUpper())
                        {
                            currMusicInfo.TrackID = info;
                        }
                        else if (type.ToUpper() == InfoTypes.AlbumID.ToString().ToUpper())
                        {
                            currMusicInfo.AlbumID = info;
                        }
                        else if (type.ToUpper() == InfoTypes.ArtistID.ToString().ToUpper())
                        {
                            currMusicInfo.ArtistID = info;
                        }
                        else if (type.ToUpper() == "ERROR")
                        {
                            API.Log(API.LogType.Error, "Web Side Error:" + info);
                        }
                        else if (type.ToUpper() == "ERRORD")
                        {
                            API.Log(API.LogType.Debug, "Web Error:" + info);
                        }

                        if (type.ToUpper() != InfoTypes.Position.ToString().ToUpper() && currMusicInfo.Title != "")
                        {
                            updateDisplayedInfo();
                        }
                    }
                }
                catch (Exception e)
                {
                    API.Log(API.LogType.Error, "Error parsing data from WebNowPlaying companion");
                    API.Log(API.LogType.Debug, e.ToString());
                }
                //System.Diagnostics.Debug.WriteLine(arg.Data);
                //API.Log(API.LogType.Debug, arg.Data);
            }

            /// <summary>
            /// Get the total number of milliseconds of a timestamp
            /// </summary>
            /// <param name="timeStamp">The Timestamp (formatted as: hh:mm:ss.fff)</param>
            /// <returns>Total number of milliseconds</returns>
            private int getMsFromTimeStamp(string timeStamp) {
                if (timeStamp.Count(c => c == ':') == 1) {
                    // We only have mm:ss(.fff). TimeSpan.Parse() will assume this is hh:mm, so we have to prepend 00 as the hh value to the string.
                    timeStamp = $"00:{timeStamp}";
                }
                
                // Parse the string into a timespan object
                return (int) TimeSpan.Parse(timeStamp).TotalMilliseconds;
            }

            protected override void OnOpen()
            {
                base.OnOpen();
                
                musicInfo.GetOrAdd(this.ID, new MusicInfo());
            }
            protected override void OnClose(CloseEventArgs e)
            {
                base.OnClose(e);

                MusicInfo temp;
                //If removing the last index in the update list and there is one before it download album art 
                musicInfo.TryRemove(this.ID, out temp);
                if (displayedMusicInfo.ID == temp.ID)
                {
                    updateDisplayedInfo();
                }
            }
            public void SendMessage(string stringToSend)
            {
                Sessions.Broadcast(stringToSend);
            }
        }

        public static void updateDisplayedInfo()
        {
            try
            {
                var iterableDictionary = musicInfo.OrderByDescending(key => key.Value.TimeStamp);
                bool suitableMatch = false;

                foreach (KeyValuePair<string, MusicInfo> item in iterableDictionary)
                {
                    //No need to check title since timestamp is only set when title is set
                    //@TODO Add visibility pass to extension and also check it
                    if (item.Value.State == 1 && item.Value.Volume >= 1)
                    {
                        if (displayedMusicInfo.ID != item.Value.ID)
                        {
                            if (item.Value.CoverByteArr.Length > 0)
                            {
                                Thread t = new Thread(() => WriteStream(item.Value.ID, item.Value.CoverByteArr));
                                t.Start();
                            }
                        }
                        displayedMusicInfo = item.Value;
                        suitableMatch = true;
                        //If match found break early which should be always very early
                        break;
                    }
                }

                if (!suitableMatch)
                {
                    MusicInfo fallbackInfo = iterableDictionary.FirstOrDefault().Value;
                    {
                        if (fallbackInfo == null)
                        {
                            fallbackInfo = new MusicInfo();
                        }

                        if (displayedMusicInfo.ID != fallbackInfo.ID)
                        {
                            if (fallbackInfo.CoverByteArr.Length > 0)
                            {
                                Thread t = new Thread(() => WriteStream(fallbackInfo.ID, fallbackInfo.CoverByteArr));
                                t.Start();
                            }
                        }
                        displayedMusicInfo = fallbackInfo;
                    }
                }
            }
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "Error finding new music info to display");
                API.Log(API.LogType.Debug, e.ToString());
            }
        }

        //For downloading the image, called in a thread in the onMessage for the websocket
        public static void GetImageFromUrl(string id, string url)
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
                            //@TODO is this null set needed?
                            currMusicInfo.Cover = null;
                            currMusicInfo.CoverByteArr = image;
                        }

                        //If this image comes from the same ID as the current displayed image go on ahead and write to disk
                        if (id == displayedMusicInfo.ID)
                        {
                            WriteStream(id, image);
                        }

                        //Only set web address after image has been written to disk
                        if(currMusicInfo != null)
                        {
                            currMusicInfo.CoverWebAddress = url;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "Unable to get album art from: " + url);
                API.Log(API.LogType.Debug, e.ToString());
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

        private static void WriteStream(string id, Byte[] image)
        {
            try
            {
                if (CoverOutputLocation == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Rainmeter/WebNowPlaying/cover.png")
                {
                    // Make sure the path folder exists if using it
                    System.IO.Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Rainmeter/WebNowPlaying");
                }

                // Write stream to file
                FileStream fs = new FileStream(CoverOutputLocation, FileMode.Create, FileAccess.Write, FileShare.Read);
                BinaryWriter bw = new BinaryWriter(fs);
                try
                {
                    bw.Write(image);
                }
                catch (Exception e)
                {
                    bw.Close();
                    fs.Close();
                    throw e;
                }
                finally
                {
                    bw.Close();
                    fs.Close();
                }

                MusicInfo lastUpdateMusicInfo;
                if (musicInfo.TryGetValue(id, out lastUpdateMusicInfo))
                {
                    lastUpdateMusicInfo.Cover = CoverOutputLocation;
                }
            }
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "Unable to download album art to: " + CoverOutputLocation);
                API.Log(API.LogType.Debug, e.ToString());
            }
        }

        static int measureCount = 0;

        internal Measure(Rainmeter.API api)
        {
            ++measureCount;
            try
            {
                if (rainmeterFileSettingsLocation != api.GetSettingsFile())
                {
                    rainmeterFileSettingsLocation = api.GetSettingsFile();
                }

                if (wssv == null)
                {
                    //@TODO Declare on reload so that custom ports can be allowed?
                    wssv = new WebSocketServer(8974);
                }

                if (wssv.IsListening == false)
                {
                    wssv.AddWebSocketService<WebNowPlaying>("/");
                    wssv.Start();
                }
            }
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error starting WebNowPlaying plugin");
                API.Log(API.LogType.Debug, "Error:" + e.ToString());
            }
        }

        internal virtual void Dispose()
        {
            --measureCount;
            if (wssv.IsListening && measureCount == 0)
            {
                wssv.Stop();
            }
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
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "WebNowPlaying.dll - Unknown PlayerType:" + playerTypeString);
                API.Log(API.LogType.Debug, "Error:" + e.ToString());
                playerType = InfoTypes.Status;
            }
        }

        internal void ExecuteBang(string args)
        {
            try
            {
                string bang = args.ToLowerInvariant();

                //@TODO Implement keeping of more than just the last update song
                WebSocketServiceHost host;

                if (displayedMusicInfo.ID != "")
                {
                    if (bang.Equals("playpause"))
                    {
                        wssv.WebSocketServices.TryGetServiceHost("/", out host);
                        host.Sessions.SendTo("PlayPause", displayedMusicInfo.ID);
                    }
                    else if (bang.Equals("next"))
                    {
                        wssv.WebSocketServices.TryGetServiceHost("/", out host);
                        host.Sessions.SendTo("next", displayedMusicInfo.ID);
                    }
                    else if (bang.Equals("previous"))
                    {
                        wssv.WebSocketServices.TryGetServiceHost("/", out host);
                        host.Sessions.SendTo("previous", displayedMusicInfo.ID);
                    }
                    else if (bang.Equals("repeat"))
                    {
                        wssv.WebSocketServices.TryGetServiceHost("/", out host);
                        host.Sessions.SendTo("repeat", displayedMusicInfo.ID);
                    }
                    else if (bang.Equals("shuffle"))
                    {
                        wssv.WebSocketServices.TryGetServiceHost("/", out host);
                        host.Sessions.SendTo("shuffle", displayedMusicInfo.ID);
                    }
                    else if (bang.Equals("togglethumbsup"))
                    {
                        wssv.WebSocketServices.TryGetServiceHost("/", out host);
                        host.Sessions.SendTo("togglethumbsup", displayedMusicInfo.ID);
                    }
                    else if (bang.Equals("togglethumbsdown"))
                    {
                        wssv.WebSocketServices.TryGetServiceHost("/", out host);
                        host.Sessions.SendTo("togglethumbsdown", displayedMusicInfo.ID);
                    }
                    else if (bang.Contains("rating"))
                    {
                        wssv.WebSocketServices.TryGetServiceHost("/", out host);
                        if (bang.Equals("rating"))
                        {
                            host.Sessions.SendTo("rating", displayedMusicInfo.ID);
                        }
                        else
                        {
                            try
                            {

                                host.Sessions.SendTo("rating " + Convert.ToInt16(bang.Substring(bang.LastIndexOf(" ") + 1)), displayedMusicInfo.ID);
                            }
                            catch
                            {
                                API.Log(API.LogType.Error, "WebNowPlaing.dll - rating number not recognized assuming no rating");
                                host.Sessions.SendTo("rating 0", displayedMusicInfo.ID);
                            }
                        }
                    }
                    else if (bang.Contains("setposition "))
                    {
                        try
                        {
                            //SetPosition gives the time in seconds, SetProgress gives it as a percent (0-1)
                            if (bang.Contains("-"))
                            {
                                int newTime = displayedMusicInfo.PositionSec - Convert.ToInt32(Convert.ToDouble(args.Substring(bang.IndexOf("-") + 1)) / 100.0 * displayedMusicInfo.DurationSec);

                                if (newTime < 0) { newTime = 0; }

                                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                                host.Sessions.SendTo("SetPosition " + newTime + ":SetProgress " + (double)newTime / displayedMusicInfo.DurationSec + ":", displayedMusicInfo.ID);
                            }
                            else if (bang.Contains("+"))
                            {
                                int newTime = displayedMusicInfo.PositionSec + Convert.ToInt32(Convert.ToDouble(args.Substring(bang.IndexOf("+") + 1)) / 100.0 * displayedMusicInfo.DurationSec);

                                if (newTime < 0) { newTime = 0; }

                                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                                host.Sessions.SendTo("SetPosition " + newTime + ":SetProgress " + (double)newTime / displayedMusicInfo.DurationSec + ":", displayedMusicInfo.ID);

                            }
                            else
                            {
                                int newTime = Convert.ToInt32(Convert.ToDouble(args.Substring(bang.IndexOf("setposition ") + 12)) / 100.0 * displayedMusicInfo.DurationSec);

                                if (newTime < 0) { newTime = 0; }

                                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                                host.Sessions.SendTo("SetPosition " + newTime + ":SetProgress " + (double)newTime / displayedMusicInfo.DurationSec + ":", displayedMusicInfo.ID);

                            }
                        }
                        catch
                        {
                            API.Log(API.LogType.Error, "WebNowPlaing.dll - SetPosition argument could not be converted to a decimal: " + args);
                        }
                    }
                    else if (bang.Contains("setvolume "))
                    {
                        try
                        {
                            if (bang.Contains("-"))
                            {
                                double newVolume = displayedMusicInfo.Volume - Convert.ToDouble(bang.Substring(bang.IndexOf("-") + 1));

                                if (newVolume > 100) { newVolume = 100; }
                                else if (newVolume < 0) { newVolume = 0; }

                                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                                host.Sessions.SendTo("SetVolume " + newVolume, displayedMusicInfo.ID);

                            }
                            else if (bang.Contains("+"))
                            {
                                double newVolume = displayedMusicInfo.Volume + Convert.ToDouble(bang.Substring(bang.IndexOf("+") + 1));

                                if (newVolume > 100) { newVolume = 100; }
                                else if (newVolume < 0) { newVolume = 0; }

                                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                                host.Sessions.SendTo("SetVolume " + newVolume, displayedMusicInfo.ID);
                            }
                            else
                            {
                                double newVolume = Convert.ToDouble(bang.Substring(bang.IndexOf(" ") + 1));

                                if (newVolume > 100) { newVolume = 100; }
                                else if (newVolume < 0) { newVolume = 0; }

                                wssv.WebSocketServices.TryGetServiceHost("/", out host);
                                host.Sessions.SendTo("SetVolume " + newVolume, displayedMusicInfo.ID);

                            }
                        }
                        catch (Exception e)
                        {
                            API.Log(API.LogType.Error, "WebNowPlaing.dll - SetVolume argument could not be converted to a decimal: " + args);
                            API.Log(API.LogType.Debug, e.Data.ToString());
                        }
                    }
                    else
                    {
                        API.Log(API.LogType.Error, "WebNowPlaying.dll - Unknown bang:" + args);
                    }
                }
            }
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error using bang:" + args);
                API.Log(API.LogType.Debug, "Error:" + e.ToString());
            }
        }

        internal virtual double Update()
        {
            try
            {
                switch (playerType)
                {
                    case InfoTypes.State:
                        return displayedMusicInfo.State;
                    case InfoTypes.Status:
                        //@TODO have this possibly be per website
                        return wssv.WebSocketServices.SessionCount > 0 ? 1 : 0;
                    case InfoTypes.Volume:
                        return displayedMusicInfo.Volume;
                    case InfoTypes.Rating:
                        return displayedMusicInfo.Rating;
                    case InfoTypes.Repeat:
                        return displayedMusicInfo.Repeat;
                    case InfoTypes.Shuffle:
                        return displayedMusicInfo.Shuffle;
                    case InfoTypes.Progress:
                        return displayedMusicInfo.Progress;
                    case InfoTypes.Position:
                        return displayedMusicInfo.PositionSec;
                    case InfoTypes.PositionMs:
                        return displayedMusicInfo.PositionMs == -1
                            ? displayedMusicInfo.PositionSec * 1000
                            : displayedMusicInfo.PositionMs;
                    case InfoTypes.Duration:
                        return displayedMusicInfo.DurationSec;
                    case InfoTypes.DurationMs:
                        return displayedMusicInfo.DurationMs == -1
                            ? displayedMusicInfo.DurationSec * 1000
                            : displayedMusicInfo.DurationMs;
                }
            }
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error doing update cycle");
                API.Log(API.LogType.Debug, "Error:" + e.ToString());
            }

            return 0.0;
        }

        internal string GetString()
        {
            try
            {
                switch (playerType)
                {
                    case InfoTypes.Player:
                        return displayedMusicInfo.Player;
                    case InfoTypes.Title:
                        return displayedMusicInfo.Title;
                    case InfoTypes.Artist:
                        return displayedMusicInfo.Artist;
                    case InfoTypes.Album:
                        return displayedMusicInfo.Album;
                    case InfoTypes.Cover:
                        if (displayedMusicInfo.Cover != null && displayedMusicInfo.Cover.Length > 0)
                        {
                            return displayedMusicInfo.Cover;
                        }
                        else if (CoverDefaultLocation != null && CoverOutputLocation.Length > 0)
                        {
                            return CoverDefaultLocation;
                        }
                        return CoverOutputLocation;
                    case InfoTypes.CoverWebAddress:
                        return displayedMusicInfo.CoverWebAddress;
                    case InfoTypes.Position:
                        return displayedMusicInfo.Position.Remove(displayedMusicInfo.Position.IndexOf('.'));
                    case InfoTypes.PositionMs:
                        return displayedMusicInfo.Position;
                    case InfoTypes.Duration:
                        return displayedMusicInfo.Duration.Remove(displayedMusicInfo.Duration.IndexOf('.'));
                    case InfoTypes.DurationMs:
                        return displayedMusicInfo.Duration;
                }
            }
            catch (Exception e)
            {
                API.Log(API.LogType.Error, "WebNowPlaying.dll - Error doing getString cycle");
                API.Log(API.LogType.Debug, "Error:" + e.ToString());
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
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Dispose();

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

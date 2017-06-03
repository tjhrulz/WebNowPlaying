# Message Passing for Rainmeter
A websocket plugin for Rainmeter to allow communication with other programs such as Wallpaper Engine.
Example of it in action: https://streamable.com/17mmz

### Current State:
 - Full support for multiple websocket addresses and multiple connections on each address
 - On websocket Open, Close, and Message command options (Can even use dynamic variables)
 - Get connected client count from measure's decimal value
 
### Future Additions
 - Support for changing Wallpaper Engine layout (Will either require waiting till Wallpaper Engine supports external plugins or admin rights)
 - Support for Video and Scene layout communication (Will likely require waiting till Wallpaper Engine supports external plugins)
 - Support for changing the port
 - Readd support for every message also being sent on the / channel
 
 ## Measure Options
 - `Plugin=WebNowPlaying` - The name of the plugin is WebNowPlaying, any WebNowPlaying measure will have the value of the number of clients connected to it
 - `Name` - Name of the service to run, adds / to the beginning if you do not.  
   I recommend using unique names if you only want your info. So a name of tjMusicInfo would get a url of ws://127.0.0.1:58932/tjMusicInfo in the program you are communicating to
 - `Port` - Port to use, is ignored right now and is always 58932
 - `OnOpen` - A rainmeter bang to execute when connection is first made  
   Note: Will not fire if connection already existed and was opened when your websocket is created
 - `OnClose` - A rainmeter bang to execute on connection being closed
   Note: Only fires with the connection is closed, does not fire if it never opens
 - `OnMessage` - A rainmeter bang to execute whenever a message is received with your service name  
   Note: Add $Message$ to the command to have it be replaced with the message contents

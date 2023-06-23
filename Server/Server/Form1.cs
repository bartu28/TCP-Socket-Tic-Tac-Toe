using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace server
{

    public class TicTacToeBoard
    {
        private char[] board;

        public TicTacToeBoard()
        {
            board = new char[9];
            for (int i = 0; i < 9; i++)
            {
                board[i] = (char)('1' + i);
            }

        }

        public bool MakeMove(int position, char symbol)
        {
            if (position < 1 || position > 9)
            {
                return false;
            }

            if (board[position - 1] == 'X' || board[position - 1] == 'O')
            {
                return false;
            }

            board[position - 1] = symbol;
            return true;
        }

        public override string ToString()
        {
            return string.Join(" | ", board.Take(3)) + Environment.NewLine
                 + "---------" + Environment.NewLine
                 + string.Join(" | ", board.Skip(3).Take(3)) + Environment.NewLine
                 + "---------" + Environment.NewLine
                 + string.Join(" | ", board.Skip(6).Take(3));
        }

        public bool CheckWin()  //cheking if there is a winner
        {
            
            for (int i = 0; i < 9; i += 3) //cheking the horizontal lines for a winner
            {
                if (board[i] == board[i + 1] && board[i + 1] == board[i + 2])
                {
                    return true;
                }
            }

            
            for (int i = 0; i < 3; i++) //cheking the vertical lines for a winner
            {
                if (board[i] == board[i + 3] && board[i + 3] == board[i + 6])
                {
                    return true;
                }
            }

            //cheking the diogonal lines for a winner
            if ((board[0] == board[4] && board[4] == board[8]) || (board[2] == board[4] && board[4] == board[6]))
            {
                return true;
            }

            return false;
        }

        public bool CheckDraw() //check if all the board is used so it is a draw
        {
            return !board.Any(c => char.IsDigit(c));
        }
    }

    public partial class Form1 : Form
    {
        TicTacToeBoard gameBoard;
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Dictionary<string, Socket> clients = new Dictionary<string, Socket>();
        Dictionary<string, bool> startMessages = new Dictionary<string, bool>();

        string currentPlayerTurn;
        Dictionary<string, Scores> scores_dic = new Dictionary<string, Scores>();

        //static Mutex mutex = new Mutex();
        bool terminating = false;
        bool listening = false;
        bool start_right = true;
        bool firsttime = true;
        int xpos = 0;
        // Struct to represent the associated values
        struct Scores
        {
            public int Wins { get; set; }
            public int Losses { get; set; }
            public int Draws { get; set; }

            public Scores(int wins, int losses, int draws)
            {
                Wins = wins;
                Losses = losses;
                Draws = draws;
            }
        }

        int numberOfGamePlayed = 0;
        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }
        private void richTextBoxLog_TextChanged(object sender, EventArgs e)
        {
            // set the current caret position to the end
            richTextBoxLog.SelectionStart = richTextBoxLog.Text.Length;
            // scroll it automatically
            richTextBoxLog.ScrollToCaret();
        }
        private void button_listen_Click(object sender, EventArgs e)
        {
            int serverPort;

            if(Int32.TryParse(textBox_port.Text, out serverPort))
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
                serverSocket.Bind(endPoint);
                serverSocket.Listen(3);

                listening = true;
                button_listen.Enabled = false;
                

                Thread acceptThread = new Thread(Accept);
                acceptThread.Start();

                richTextBoxLog.AppendText("Started listening on port: " + serverPort + "\n");

            }
            else
            {
                richTextBoxLog.AppendText("Please check port number \n");
            }
        }

        private void Accept()
        {
            while (listening)
            {
                try
                {
                    Socket newClient = serverSocket.Accept();

                    if (clients.Count < 4) //step2 reqirement
                    {
                        // recieving client name
                        byte[] nameBuffer = new byte[64];
                        int nameSize = newClient.Receive(nameBuffer);
                        string clientName = Encoding.Default.GetString(nameBuffer, 0, nameSize);

                        // checking if client name already exists
                        if (!clients.ContainsKey(clientName))
                        {
                            clients.Add(clientName, newClient);
                            scores_dic[clientName] = new Scores(0,0,0); /// adding new clients to the dictionary of scores


                            startMessages.Add(clientName, false);

                            richTextBoxLog.AppendText($"{clientName} connected.\n");

                            string connectedbuffermessage = $"{clientName} connected.\n";
                            Byte[] connectedbuffer = Encoding.Default.GetBytes(connectedbuffermessage.PadRight(64, '\0'));
                            newClient.Send(connectedbuffer);
                            

                            Thread receiveThread = new Thread(() => Receive(newClient, clientName));
                            receiveThread.Start();
                        }
                        else
                        {
                            richTextBoxLog.AppendText($"A client tried to connect with an existing name ({clientName}). Connection rejected.\n");
                            string nameExistsMessage = "This name is already taken, please choose another name.";
                            Byte[] nameExistsBuffer = Encoding.Default.GetBytes(nameExistsMessage.PadRight(64, '\0'));
                            newClient.Send(nameExistsBuffer);
                            newClient.Close();
                        }
                    }
                    else
                    {
                        string serverfullmessage = "A client tried to connect, but the server is full.\n";
                        richTextBoxLog.AppendText(serverfullmessage);
                        Byte[] serverfullBuffer = Encoding.Default.GetBytes(serverfullmessage.PadRight(64, '\0'));
                        newClient.Send(serverfullBuffer);
                        newClient.Close();
                    }
                }
                catch
                {
                    if (terminating)
                    {
                        listening = false;
                    }
                    else
                    {
                        richTextBoxLog.AppendText("The socket stopped working.\n");
                    }
                }
            }
        }



        private void Receive(Socket thisClient, string clientName) // recieve methods listens the server and reponds to to incomming messages accordingly
        {
            bool connected = true;
           
            while (connected && !terminating)
            {
                try
                {
                    Byte[] buffer = new Byte[64];
                    int receivedDataLength = thisClient.Receive(buffer);

                    if (receivedDataLength > 0)
                    {
                        string incomingMessage = Encoding.Default.GetString(buffer);
                        incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
                        richTextBoxLog.AppendText($"{clientName}: {incomingMessage}\n");

                        if (incomingMessage == "start" && firsttime)
                        {
                            startMessages[clientName] = true;

                            if (TwoPlayersSentStart())
                            {
                                string gameStartedMessage = "Game started.\n";
                                Byte[] gameStartedBuffer = Encoding.Default.GetBytes(gameStartedMessage);

                                foreach (Socket client in clients.Values)
                                {
                                    client.Send(gameStartedBuffer);
                                }

                                gameBoard = new TicTacToeBoard();
                                currentPlayerTurn = clients.Keys.First();
                                richTextBoxLog.AppendText("Both players joined. Game started.\n");
                                richTextBoxLog.AppendText("Initial board:\n" + gameBoard.ToString() + "\n");
                                richTextBoxLog.AppendText($"It's {currentPlayerTurn}'s turn.\n");

                                // send initial board to both clients
                                string initialBoardMessage = gameBoard.ToString();
                                Byte[] initialBoardBuffer = Encoding.Default.GetBytes(initialBoardMessage.PadRight(64, '\0'));
                                foreach (Socket client in clients.Values)
                                {
                                    client.Send(initialBoardBuffer);
                                }

                                string turnMessage = $"It's {currentPlayerTurn}'s turn.\n";
                                Byte[] turnBuffer = Encoding.Default.GetBytes(turnMessage);
                                foreach (Socket client in clients.Values)
                                {
                                    client.Send(turnBuffer);
                                }
                            }
                        }
                        else if (incomingMessage == "start")
                        {
                            //mutex.WaitOne();
                            if (start_right) {
                                startMessages[clientName] = true;
                            }

                            Byte[] boardBuffer = Encoding.Default.GetBytes("last position:\n" + gameBoard.ToString());
                            thisClient.Send(boardBuffer);


                        }
                        else if (TwoPlayersSentStart()/*&& startMessages[clientName]*/)
                        {
                            firsttime = false;
                            //start_right = false; // it used for concurency problem
                            int position;
                            if (Int32.TryParse(incomingMessage, out position))
                            {
                                if (clientName == currentPlayerTurn)
                                {
                                    char symbol = clientName == clients.Keys.ElementAt(xpos) ? 'X' : 'O';
                                    if (gameBoard.MakeMove(position, symbol))
                                    {
                                        string updatedBoard = gameBoard.ToString();
                                        Byte[] boardBuffer = Encoding.Default.GetBytes(updatedBoard);
                                        foreach (Socket client in clients.Values)
                                        {
                                            client.Send(boardBuffer);
                                        }
                                        richTextBoxLog.AppendText($"{clientName} made a move:\n{updatedBoard}\n");
                                        if (gameBoard.CheckWin())
                                        {
                                          
                                            if (scores_dic.ContainsKey(clientName))
                                            {
                                                Scores scoreOfclient = scores_dic[clientName];
                                                scoreOfclient.Wins += 1;
                                                scores_dic[clientName] = scoreOfclient;
                                            }
                                            
                                           
                                            string winMessage = $"{clientName} wins! You can continue playing. It's {currentPlayerTurn}'s turn.\nWins Losses Draws\n";

                                            string loser = clients.Keys.First(x => x != clientName);
                                            if (scores_dic.ContainsKey(loser))
                                            {
                                                Scores scoreOfclient = scores_dic[loser];
                                                scoreOfclient.Losses += 1;
                                                scores_dic[loser] = scoreOfclient;
                                            }

                                            string key; Scores score;
                                            foreach (KeyValuePair<string, Scores> entry in scores_dic)
                                            {
                                                key = entry.Key;
                                                score = entry.Value;
                                                winMessage += $"{key}:  {score.Wins}  ,  {score.Losses}  , {score.Draws} .\n";
                                            }
                                            numberOfGamePlayed += 1;

                                            winMessage += $"Number of Game Played: {numberOfGamePlayed}\n";

                                            Byte[] winBuffer = Encoding.Default.GetBytes(winMessage);
                                            foreach (Socket client in clients.Values)
                                            {
                                                client.Send(winBuffer);
                                            }
                                            richTextBoxLog.AppendText(winMessage + "\n");
                                            firsttime = true;

                                            gameBoard = new TicTacToeBoard();
                                            Byte[] initialBoardBufferwin = Encoding.Default.GetBytes(gameBoard.ToString().PadRight(64, '\0'));
                                            foreach (Socket client in clients.Values)
                                            {
                                                client.Send(initialBoardBufferwin);
                                            }

                                        }
                                        else if (gameBoard.CheckDraw())
                                        {
                                            if (scores_dic.ContainsKey(clientName))
                                            {
                                                Scores scoreOfclient = scores_dic[clientName];
                                                scoreOfclient.Draws += 1;
                                                scores_dic[clientName] = scoreOfclient;
                                            }
                                            string other_player = clients.Keys.First(x => x != clientName);
                                            numberOfGamePlayed += 1;
                                            if (scores_dic.ContainsKey(other_player))
                                            {
                                                Scores scoreOfclient = scores_dic[other_player];
                                                scoreOfclient.Draws += 1;
                                                scores_dic[other_player] = scoreOfclient;
                                            }
                                            string drawMessage = $"It's a draw! You can continue playing. It's {currentPlayerTurn}'s turn.\nWins Losses Draws\n";

                                            string key; Scores score;
                                            foreach (KeyValuePair<string, Scores> entry in scores_dic)
                                            {
                                                key = entry.Key;
                                                score = entry.Value;
                                                drawMessage += $"{key}:  {score.Wins}  ,  {score.Losses}  , {score.Draws} .\n";
                                            }
                                            drawMessage += $"Number of Game Played: {numberOfGamePlayed}\n";
                                            Byte[] drawBuffer = Encoding.Default.GetBytes(drawMessage);
                                            foreach (Socket client in clients.Values)
                                            {
                                                client.Send(drawBuffer);
                                            }
                                            richTextBoxLog.AppendText(drawMessage + "\n");
                                            firsttime = true;

                                            gameBoard = new TicTacToeBoard();
                                            Byte[] initialBoardBufferdraw = Encoding.Default.GetBytes(gameBoard.ToString().PadRight(64, '\0'));
                                            foreach (Socket client in clients.Values)
                                            {
                                                client.Send(initialBoardBufferdraw);
                                            }



                                        }
                                        else
                                        {
                                            if (clients.Count < 2)
                                            {
                                                Byte[] notenoughplayerbuffer = Encoding.Default.GetBytes("There are not enough players.");
                                                foreach (Socket client in clients.Values)
                                                {
                                                    client.Send(notenoughplayerbuffer);
                                                }

                                            }
                                            while (true)
                                            {
                                                if (clients.Count > 1)
                                                {
                                                    currentPlayerTurn = clients.Keys.First(x => x != clientName);
                                                    break;
                                                }
                                            }

                                            richTextBoxLog.AppendText($"It's {currentPlayerTurn}'s turn.\n");

                                            string turnMessage = $"It's {currentPlayerTurn}'s turn.\n";
                                            Byte[] turnBuffer = Encoding.Default.GetBytes(turnMessage);
                                            foreach (Socket client in clients.Values)
                                            {
                                                client.Send(turnBuffer);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // error message when trying to make the same move
                                        string errorMessage = "The position is already occupied.";
                                        Byte[] errorBuffer = Encoding.Default.GetBytes(errorMessage);
                                        thisClient.Send(errorBuffer);
                                    }
                                }
                                else
                                {
                                    string waitMessage = "Wait for your turn.";
                                    Byte[] waitBuffer = Encoding.Default.GetBytes(waitMessage);
                                    thisClient.Send(waitBuffer);
                                }
                            }
                        }
                        else
                        {
                            Byte[] waitingplayersbuffere = Encoding.Default.GetBytes("Waiting for start.");

                            foreach (Socket client in clients.Values)
                            {
                                client.Send(waitingplayersbuffere);
                            }
                        }
                    }
                }
                catch
                {
                    if (!terminating)
                    {
                        //start_right = true;
                        richTextBoxLog.AppendText($"{clientName} has disconnected.\n");
                    }
                    if (clients.Count == 1)
                    {
                        thisClient.Close();
                        clients.Remove(clientName);
                        startMessages.Remove(clientName);
                        connected = false;
                    }
                    else
                    {

                        //we have a problem with xpos and currentplayer.
                        int thingToDo = -1;
                        if (currentPlayerTurn == clientName)//this turns player left, might be first or second
                        {
                            if (clients.Values.ToList().IndexOf(clients[clientName]) == 0)//was0 index       this turns player
                            {
                                xpos = xpos == 0 ? 1 : 0;
                                thingToDo = 0;
                            }
                            else//was1 index          this turns player
                            {
                                //keep it same
                                thingToDo = 1;
                            }
                        }
                        if (currentPlayerTurn == clients.Keys.First(x => x != clientName))//next turns player left, might be first or second
                        {
                            if (clients.Values.ToList().IndexOf(clients[clientName]) == 0)//was0 index      next turns player
                            {
                                xpos = xpos == 0 ? 1 : 0;
                                thingToDo = 2;
                            }
                            else//was1 index           next turns player
                            {
                                thingToDo = 3;
                                //keep it same
                            }
                        }

                        thisClient.Close();
                        clients.Remove(clientName);
                        startMessages.Remove(clientName);
                        connected = false;
                        // start_right = true;
                        Byte[] disconnectedbuffer = Encoding.Default.GetBytes($"{clientName} has disconnected.\n");

                        foreach (Socket client in clients.Values)
                        {
                            client.Send(disconnectedbuffer);
                        }
                        if (clients.Count < 2)
                        {
                            Byte[] waitingbufferr = Encoding.Default.GetBytes($"Not enough players.\n");
                            foreach (Socket client in clients.Values)
                            {
                                client.Send(waitingbufferr);
                            }
                        }
                        while (true)
                        {
                            if (clients.Count > 1)
                            {
                                if (thingToDo == 0) { currentPlayerTurn = clients.Keys.ElementAt(1); }//so complicated... shouldnt have done this.
                                else if (thingToDo == 1) { currentPlayerTurn = clients.Keys.ElementAt(1); }
                                else if (thingToDo == 2) { currentPlayerTurn = clients.Keys.ElementAt(0); }
                                else if (thingToDo == 3) { currentPlayerTurn = clients.Keys.ElementAt(0); }

                                break;
                            }
                        }
                        Byte[] waitingbuffer = Encoding.Default.GetBytes($"Waiting for {currentPlayerTurn} to continue.\n");
                        foreach (Socket client in clients.Values)
                        {
                            client.Send(waitingbuffer);
                        }
                    }                  
                }
            }
        }



        private bool TwoPlayersSentStart() // checks if first two players joined with the start message
        {
            Byte[] waitingplayersbuffer = Encoding.Default.GetBytes("waiting for the first two players to type start");



            foreach (bool startStatus in startMessages.Values.Take(2))
            {
                if (!startStatus)
                {
                    foreach (Socket client in clients.Values)
                    {
                        client.Send(waitingplayersbuffer);
                    }



                    return false;
                }
            }


            return true;
        }



        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            listening = false;
            terminating = true;
            Environment.Exit(0);
        }

        
    }
}

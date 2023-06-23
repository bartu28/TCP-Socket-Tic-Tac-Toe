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

        bool terminating = false;
        bool listening = false;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
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

                    if (clients.Count < 2)
                    {
                        // recieving client name
                        byte[] nameBuffer = new byte[64];
                        int nameSize = newClient.Receive(nameBuffer);
                        string clientName = Encoding.Default.GetString(nameBuffer, 0, nameSize);

                        // checking if client name already exists
                        if (!clients.ContainsKey(clientName))
                        {
                            clients[clientName] = newClient;
                            startMessages[clientName] = false;

                            richTextBoxLog.AppendText($"{clientName} connected.\n");

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

                        if (incomingMessage == "start")
                        {
                            startMessages[clientName] = true;

                            if (AllClientsSentStart())
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
                        else if (AllClientsSentStart())
                        {
                            int position;
                            if (Int32.TryParse(incomingMessage, out position))
                            {
                                if (clientName == currentPlayerTurn)
                                {
                                    char symbol = clientName == clients.Keys.First() ? 'X' : 'O';
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
                                            string winMessage = $"{clientName} wins!";
                                            Byte[] winBuffer = Encoding.Default.GetBytes(winMessage);
                                            foreach (Socket client in clients.Values)
                                            {
                                                client.Send(winBuffer);
                                            }
                                            richTextBoxLog.AppendText(winMessage + "\n");
                                            return;
                                        }
                                        else if (gameBoard.CheckDraw())
                                        {
                                            string drawMessage = "It's a draw!";
                                            Byte[] drawBuffer = Encoding.Default.GetBytes(drawMessage);
                                            foreach (Socket client in clients.Values)
                                            {
                                                client.Send(drawBuffer);
                                            }
                                            richTextBoxLog.AppendText(drawMessage + "\n");
                                            return;
                                        }
                                        else
                                        {
                                            currentPlayerTurn = clients.Keys.First(x => x != clientName);
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
                    }
                }
                catch
                {
                    if (!terminating)
                    {
                        richTextBoxLog.AppendText($"{clientName} has disconnected.\n");
                    }
                    thisClient.Close();
                    clients.Remove(clientName);
                    startMessages.Remove(clientName);
                    connected = false;
                }
            }
        }




        private bool AllClientsSentStart() // cheks if both players joined with the strat message
        {
            if (clients.Count < 2)
            {
                return false;
            }

            foreach (bool startStatus in startMessages.Values)
            {
                if (!startStatus)
                {
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

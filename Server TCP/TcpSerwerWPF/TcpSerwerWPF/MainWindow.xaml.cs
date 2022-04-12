using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.ComponentModel;
using System.Windows.Controls;
using mshtml;

namespace TcpSerwerWPF
{
    public partial class MainWindow : Window
    {
        #region variables
        private TcpListener server;
        private bool activeCall = false;
        private readonly BackgroundWorker BWConnection;
        List<User> users;
        Chat selectChat;
        User selectUser;
        List<Chat> chats;
        #endregion
        #region public functions
        public void UpdateChatsColors()
        {
            for (int i = 0; i < chats.Count; i++)
            {
                chats[i].UpdateColors();
            }
        }
        public void DelUser(Chat chat, User user)
        {
            if (user == null || chat == null) return;
            if (chat.users.IndexOf(user.nick) < 0) return;
            chat.RemoveUser(user.nick);
            user.Write("del+" + chat.name);
        }
        public void DelChat(Chat chat)
        {
            if (chat == null) return;
            if (selectChat != null)
                if(selectChat.name == chat.name && GridChat.Visibility == Visibility.Visible)
                {
                    HideGrids();
                    GridChats.Visibility = Visibility.Visible;
                }
            for (int i = 0; i < chat.users.Count; i++)
            {
                User u = FindUserByNick(chat.users[i]);
                if (u != null)
                {
                    u.Write("del+" + chat.name);
                }
            }
            for (int i = 0; i < chats.Count; i++)
            {
                if (chats[i].name == chat.name)
                {
                    chats.RemoveAt(i);
                    break;
                }
            }
            UpdateChatsList();
        }
        public void TryAddChat(User user, string name)
        {
            for (int i = 0; i < chats.Count; i++)
            {
                if (chats[i].name == name)
                {
                    return;
                }
            }
            user.Write("add+" + name);
            Chat newChat = new Chat(name, message, this);
            newChat.AddUser(user.nick);
            chats.Add(newChat);
            newChat.SetFirstAdmin(user);
            UpdateChatsList();
        }
        public bool CanAddUser(string nick)
        {
            if (nick.Length == 0) return false;
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].nick == nick) return false;
            }
            return true;
        }
        public void AddUser(User user)
        {
            users.Add(user);
            UpdateUsersList();
        }
        public void DeleteUser(string nick)
        {
            for (int i = 0; i < chats.Count; i++)
            {
                chats[i].RemoveUser(nick);
            }
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].nick == nick)
                {
                    users.RemoveAt(i);
                    break;
                }
            }
            UpdateUsersList();
        }
        public void Write(Chat chat, string text)
        {
            for (int i = 0; i < chat.users.Count; i++)
            {
                User u = FindUserByNick(chat.users[i]);
                if (u != null)
                {
                    u.Write("write+" + chat.name + "+" + text);
                }
            }
            if (selectChat != null)
            if (selectChat.name == chat.name)
            {
                selectChat.Show();
            }
        }
        public User FindUserByNick(string nick)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].nick == nick) return users[i];
            }
            return null;
        }
        public Chat FindChatByName(string name)
        {
            for(int i=0; i<chats.Count; i++)
            {
                if (chats[i].name == name) return chats[i];
            }
            return null;
        }
        #endregion
        #region Constructor + Listener
        public MainWindow()
        {
            InitializeComponent();
            ColorsManager.Load();
            ColorsManager.GenerateColorsLayout(this);
            message.LoadCompleted += delegate
            {
                HTMLDocument html = (HTMLDocument)message.Document;
                html.parentWindow.scroll(0,100000000);
            };
            users = new List<User>();
            chats = new List<Chat>();
            HideGrids();
            GridConnection.Visibility = Visibility.Visible;
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "img/logout.png"))
            {
                Uri uri = new Uri(AppDomain.CurrentDomain.BaseDirectory + "img/logout.png", UriKind.RelativeOrAbsolute);
                PanelImageClose.Source = BitmapFrame.Create(uri);
            }
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "img/download.png"))
            {
                Uri uri = new Uri(AppDomain.CurrentDomain.BaseDirectory + "img/download.png", UriKind.RelativeOrAbsolute);
                PanelImageMinimalize.Source = BitmapFrame.Create(uri);
            }
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "img/link.png"))
            {
                Uri uri = new Uri(AppDomain.CurrentDomain.BaseDirectory + "img/link.png", UriKind.RelativeOrAbsolute);
                ImageConnection.Source = BitmapFrame.Create(uri);
            }
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "img/chat.png"))
            {
                Uri uri = new Uri(AppDomain.CurrentDomain.BaseDirectory + "img/chat.png", UriKind.RelativeOrAbsolute);
                ImageChat.Source = BitmapFrame.Create(uri);
            }
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "img/avatar.png"))
            {
                Uri uri = new Uri(AppDomain.CurrentDomain.BaseDirectory + "img/avatar.png", UriKind.RelativeOrAbsolute);
                ImageUsers.Source = BitmapFrame.Create(uri);
            }
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "img/settings.png"))
            {
                Uri uri = new Uri(AppDomain.CurrentDomain.BaseDirectory + "img/settings.png", UriKind.RelativeOrAbsolute);
                ImageSettings.Source = BitmapFrame.Create(uri);
            }
            BWConnection = new BackgroundWorker();
            BWConnection.DoWork += delegate {
                IPAddress ip = null;
                string text = "";
                Dispatcher.Invoke(new Action(delegate {
                    text = InputAddress.Text;
                }));
                Console.WriteLine(text);
                try
                {
                    ip = IPAddress.Parse(text);
                }
                catch
                {
                    Dispatcher.Invoke(new Action(delegate {
                        InputAddress.Text = "";
                        Show("Błędny format adresu ip");
                        Show("Serwer zakończył nasłuchiwanie");
                        activeCall = false;
                        StartButton.IsEnabled = true;
                        StopButton.IsEnabled = false;
                    }));
                    return;
                }
                int port = System.Convert.ToInt16(1410);
                try
                {
                    server = new TcpListener(ip, port);
                    server.Start();
                    while (true)
                    {
                        TcpClient client = server.AcceptTcpClient();
                        IPEndPoint iPEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                        Show("[" + iPEndPoint.ToString() + "]: Nawiązano połączenie");
                        Dispatcher.Invoke(new Action(delegate
                        {
                            try
                            {
                                new User(client, this);
                            }
                            catch { }
                        }));
                    }
                }
                catch (Exception err)
                {
                    if (activeCall)
                    {
                        Show("Błąd inicjacji serwera");
                    }
                    else
                    {
                        Show("Serwer zakończył nasłuchiwanie");
                    }
                    Console.WriteLine(err);
                    activeCall = false;
                    server.Stop();
                    Dispatcher.Invoke(new Action(delegate
                    {
                        StartButton.IsEnabled = true;
                        StopButton.IsEnabled = false;
                    }));
                }
            };
        }
        private void Show(string s)
        {
            Dispatcher.Invoke(new Action(delegate {
                Info.Items.Add(s);
            }));
        }
        #endregion
        #region Connection
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Info.Items.Add("Start nasłuchiwania...");
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                activeCall = true;
                BWConnection.RunWorkerAsync();
            }
            catch { }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            activeCall = false;
            server.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
        #endregion
        #region Sending Message
        void SendMessage()
        {
            selectChat.AdminWrite(InputMessage.Text);
            InputMessage.Text = "";
            InputMessage.Focus();
        }

        private void InputMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendMessage();
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }
        #endregion
        #region Add to chat user
        private void InputUser_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string nick = InputUser.Text;
                User user = FindUserByNick(nick);
                if (user == null) return;
                if (selectChat.users.IndexOf(user.nick) < 0)
                {
                    user.Write("add+" + selectChat.name);
                    selectChat.AddUser(user.nick);
                    InputUser.Text = "";
                    InputUser.Focus();
                }
                else
                {
                    DelUser(selectChat, user);
                    InputUser.Text = "";
                    InputUser.Focus();
                }
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string nick = InputUser.Text;
            User user = FindUserByNick(nick);
            if (user == null) return;
            if (selectChat.users.IndexOf(user.nick) < 0)
            {
                user.Write("add+" + selectChat.name);
                selectChat.AddUser(user.nick);
                InputUser.Text = "";
                InputUser.Focus();
            }
        }
        #endregion
        #region Panel
        private void PanelButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Panel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch { }
        }

        private void PanelButtonMinimalize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        #endregion
        #region Grids
        void HideGrids()
        {
            GridChat.Visibility = Visibility.Hidden;
            GridConnection.Visibility = Visibility.Hidden;
            GridUsers.Visibility = Visibility.Hidden;
            GridChats.Visibility = Visibility.Hidden;
            GridSettings.Visibility = Visibility.Hidden;
        }
        private void ButtonConnection_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideGrids();
            GridConnection.Visibility = Visibility.Visible;
        }

        private void ButtonUsers_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideGrids();
            GridUsers.Visibility = Visibility.Visible;
            UpdateUsersList();
        }

        private void ButtonChat_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideGrids();
            GridChats.Visibility = Visibility.Visible;
            UpdateChatsList();
        }

        private void ButtonSettings_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideGrids();
            GridSettings.Visibility = Visibility.Visible;
        }
        #endregion
        #region Users panel
        void UpdateUsersList()
        {
            UsersList.Items.Clear();
            for(int i=0; i<users.Count; i++)
            {
                UsersList.Items.Add(users[i].nick);
            }
            UsersList.SelectedIndex = -1;
            UsersKickUser.IsEnabled = false;
            UsersSelectUser.Text = "Wybierz użytkownika";
        }
        private void UsersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersList.SelectedIndex < 0) return;
            selectUser = users[UsersList.SelectedIndex];
            UsersKickUser.IsEnabled = true;
            UsersSelectUser.Text = "Użytkownik: " + selectUser.nick;
        }
        private void UsersKickUser_Click(object sender, RoutedEventArgs e)
        {
            selectUser.Kick();
        }
        #endregion
        #region Chat panel
        void UpdateChatsList()
        {
            ChatsList.Items.Clear();
            for (int i = 0; i < chats.Count; i++)
            {
                ChatsList.Items.Add(chats[i].name);
            }
            ChatsList.SelectedIndex = -1;
            ChatsOpenChatButton.IsEnabled = false;
            ChatsDeleteChatButton.IsEnabled = false;
            ChatsSelectChat.Text = "Wybierz czat";
            ChatsCountUsers.Text = "Liczba użytkowników";
            ChatsListUsers.Items.Clear();
        }
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateChatsList();
        }
        void UpdateListUsers()
        {
            ChatsListUsers.Items.Clear();
            for (int i = 0; i < selectChat.users.Count; i++)
            {
                ListBoxItem item = new ListBoxItem();
                item.Content = selectChat.users[i];
                ChatsListUsers.Items.Add(item);
            }
        }
        private void ChatsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChatsList.SelectedIndex < 0) return;
            selectChat = chats[ChatsList.SelectedIndex];
            ChatsOpenChatButton.IsEnabled = true;
            ChatsDeleteChatButton.IsEnabled = true;
            ChatsSelectChat.Text = "Czat: "+selectChat.name;
            ChatsCountUsers.Text = "Liczba użytkowników: "+selectChat.users.Count;
            UpdateListUsers();
        }
        private void ChatsOpenChatButton_Click(object sender, RoutedEventArgs e)
        {
            HideGrids();
            GridChat.Visibility = Visibility.Visible;
            selectChat.Show();
        }
        private void ChatsDeleteChatButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < selectChat.users.Count; i++)
            {
                User u = FindUserByNick(selectChat.users[i]);
                if (u != null)
                {
                    u.Write("del+" + selectChat.name);
                }
            }
            for (int i = 0; i < chats.Count; i++)
            {
                if (chats[i].name == selectChat.name)
                {
                    chats.RemoveAt(i);
                    break;
                }
            }
            UpdateChatsList();
        }
        private void ChatsAddChatButton_Click(object sender, RoutedEventArgs e)
        {
            string newName = ChatsAddChatInput.Text;
            ChatsAddChatInput.Text = "";
            for (int i = 0; i < chats.Count; i++)
            {
                if (chats[i].name == newName)
                {
                    return;
                }
            }
            chats.Add(new Chat(newName, message, this));
            UpdateChatsList();
        }
        private void DelUserButton_Click(object sender, RoutedEventArgs e)
        {
            User user = FindUserByNick(InputUser.Text);
            if (user != null)
            {
                DelUser(selectChat, user);
            }
        }
        private void ChangeAdminButton_Click(object sender, RoutedEventArgs e)
        {
            User user = FindUserByNick(InputUser.Text);
            if (user != null)
            {
                if (selectChat.IsUserAdmin(user))
                {
                    selectChat.Admin(user, false);
                }
                else
                {
                    selectChat.Admin(user, true);
                }
            }
        }
        #endregion
    }

    public class User
    {
        TcpClient client;
        BinaryReader reader;
        BinaryWriter writter;
        readonly BackgroundWorker BWReader;
        Action<string> Disconnect;
        public string nick { get; private set; }
        public User(TcpClient _client, MainWindow mainWindow)
        {
            client = _client;
            Disconnect = mainWindow.DeleteUser;
            BWReader = new BackgroundWorker();
            BWReader.DoWork += delegate
            {
                try
                {
                    string mess;
                    while ((mess = reader.ReadString()) != "END")
                    {
                        mainWindow.Dispatcher.Invoke(new Action(delegate{
                            Console.WriteLine(nick + "> " + mess);
                            string[] command = mess.Split('+');
                            string chatName;
                            Chat chat;
                            User user;
                            switch (command[0])
                            {
                                case "new":
                                    // nowy czat
                                    if (command.Length < 2) break;
                                    mainWindow.TryAddChat(this, mess.Substring(command[0].Length + 1));
                                    break;
                                case "write":
                                    // Pisanie
                                    if (command.Length < 3) break;
                                    chat = mainWindow.FindChatByName(command[1]);
                                    if (chat != null)
                                    {
                                        if (chat.users.IndexOf(nick) != -1)
                                        chat.Add(nick, mess.Substring(command[0].Length + command[1].Length + 2));
                                    }
                                    break;
                                case "add":
                                    // Dodawanie do czatu
                                    if (command.Length < 3) break;
                                    chat = mainWindow.FindChatByName(command[1]);
                                    if (chat != null)
                                    {
                                        if (!chat.IsUserAdmin(this)) break;
                                        string newPerson = mess.Substring(command[0].Length + command[1].Length + 2);
                                        user = mainWindow.FindUserByNick(newPerson);
                                        if (user == null) break;
                                        if (chat.UserBelong(user.nick)) break;
                                        user.Write("add+" + chat.name);
                                        chat.AddUser(user.nick);
                                    }
                                    break;
                                case "full":
                                    if (command.Length < 2) break;
                                    chatName = mess.Substring(command[0].Length + 1);
                                    chat = mainWindow.FindChatByName(chatName);
                                    if (chat == null) break;
                                    if (chat.UserBelong(nick))
                                    {
                                        Write("full+" + chat.name + "+" + chat.content);
                                    }
                                    break;
                                case "del":
                                    if (command.Length < 2) break;
                                    chatName = mess.Substring(command[0].Length + 1);
                                    chat = mainWindow.FindChatByName(chatName);
                                    if (!chat.IsUserAdmin(this)) break;
                                    mainWindow.DelChat(chat);
                                    break;
                                case "exit":
                                    if (command.Length < 2) break;
                                    chatName = mess.Substring(command[0].Length + 1);
                                    chat = mainWindow.FindChatByName(chatName);
                                    if (chat == null) break;
                                    mainWindow.DelUser(chat, this);
                                    break;
                                case "rem":
                                    if (command.Length < 3) break;
                                    chatName = command[1];
                                    string personNick = mess.Substring(command[0].Length + command[1].Length + 2);
                                    user = mainWindow.FindUserByNick(personNick);
                                    chat = mainWindow.FindChatByName(chatName);
                                    if (user == null || chat == null) break;
                                    if (!chat.IsUserAdmin(this)) break;
                                    if (chat.UserBelong(nick) && chat.UserBelong(user.nick))
                                    {
                                        mainWindow.DelUser(chat, user);
                                    }
                                    break;
                                case "admin":
                                    if (command.Length < 4) break;
                                    // admin+(add|del)+user+N
                                    bool set = command[1] == "add";
                                    string whom = command[2];
                                    chatName = command[3];
                                    chat = mainWindow.FindChatByName(chatName);
                                    chat?.Admin(this, mainWindow.FindUserByNick(whom), set);
                                    break;
                            }
                        }));
                    }
                    client.Close();
                    mainWindow.Dispatcher.Invoke(new Action(delegate
                    {
                        Disconnect(nick);
                    }));
                }
                catch
                {
                    Console.WriteLine("Koniec: " + nick);
                    client.Close();
                    mainWindow.Dispatcher.Invoke(new Action(delegate
                    {
                        Disconnect(nick);
                    }));
                }
            };
            NetworkStream ns = client.GetStream();
            reader = new BinaryReader(ns);
            writter = new BinaryWriter(ns);
            nick = reader.ReadString();
            if (mainWindow.CanAddUser(nick))
            {
                writter.Write("OK");
                mainWindow.AddUser(this);
                BWReader.RunWorkerAsync();
            }
            else
            {
                writter.Write("Taki użytkownik już istnieje!");
                client.Close();
            }
        }
        public void Kick()
        {
            client.Close();
            Disconnect(nick);
        }
        public void Write(string s)
        {
            try
            {
                writter.Write(s);
                Console.WriteLine(nick + "< " + s);
            }
            catch { }
        }
    }
    public class Chat
    {
        string before;
        string after;
        public string content { get; private set; }
        WebBrowser wb;
        public List<string> users { get; private set; }
        public List<string> admins { get; private set; }
        public string name { get; private set; }
        MainWindow main;
        public Chat(string _name,WebBrowser webBrowser, MainWindow mainWindow)
        {
            main = mainWindow;
            name = _name;
            users = new List<string>();
            admins = new List<string>();
            wb = webBrowser;
            content = "";
            //before = "<!DOCTYPE html><html><head><meta charset=\"UTF-8\"/><style>*{font-family:Consolas,monospace;font-size:16px;}.comment{color:#aaaaaa;text-align:center;}.message{padding:2px 6px;}.message .content{border:1px solid #aaaaaa;border-radius:7px;padding:10px;overflow:hidden;}.message.admin .user{font-weight:bold;color:rgb(133,34,189);}.message.admin .content{border:2px solid rgb(100,26,140);background:rgb(133,34,189);color:white;}span{display:block;}</style></head><body>";
            after = "</body></html>";
            UpdateColors();
        }
        public void UpdateColors()
        {
            ColorPackage p = ColorsManager.currentPackage;
            before = "<!DOCTYPE html><html><head><meta charset=\"UTF-8\"/><style>*{font-family:Consolas,monospace;font-size:16px;}.comment{color:#aaaaaa;text-align:center;}.message{padding:2px 6px;}img{height:20px;}"+
                ".message .content{border:1px solid #aaaaaa;border-radius:7px;padding:10px;overflow:hidden;}.message.admin .user{font-weight:bold;color:rgb("+p.darkColor.R+ "," + p.darkColor.G + "," + p.darkColor.B + ");}"+
                ".message.admin .content{border:2px solid rgb(" + p.darkColor.R + "," + p.darkColor.G + "," + p.darkColor.B + ");background:rgb(" + p.lightColor.R + "," + p.lightColor.G + "," + p.lightColor.B + ");color:white;}span{display:block;}</style></head><body>";
        }
        public void AdminWrite(string s)
        {
            string txt = ChatMessageFilter.AdminSendText(s);
            content += txt;
            main.Write(this, txt);
        }
        public void Add(string who, string s)
        {
            string txt = ChatMessageFilter.SendText(who, s);
            content += txt;
            main.Write(this, txt);
        }
        public void Clear()
        {
            content = "";
        }
        public void Show()
        {
            BrowserBehavior.SetHtml(wb, before + content + after);
        }
        public void AddUser(string s)
        {
            users.Add(s);
            string txt = ChatMessageFilter.SendComment("User " + s + " join to chat");
            content += txt;
            main.Write(this, txt);
        }
        public void RemoveUser(string s)
        {
            for(int i=0; i<users.Count; i++)
            {
                if(users[i] == s)
                {
                    string txt = ChatMessageFilter.SendComment("User " + s + " leave the chat");
                    content += txt;
                    main.Write(this, txt);
                    users.RemoveAt(i);
                    break;
                }
            }
            int adminIndex = admins.IndexOf(s);
            if (adminIndex != -1)
            {
                admins.RemoveAt(adminIndex);
            }
            if(users.Count == 0)
            {
                //main.DelChat(this);
            }
        }
        public bool UserBelong(string nick)
        {
            return users.IndexOf(nick) >= 0;
        }
        public void Admin(User who, User whom, bool set)
        {
            if (who == null || whom == null) return;
            if (users.IndexOf(who.nick) == -1 || users.IndexOf(whom.nick) == -1) return;
            if (admins.IndexOf(who.nick) == -1) return;
            if ((admins.IndexOf(whom.nick) == -1) == set)
            {
                if (set)
                {
                    admins.Add(whom.nick);
                    whom.Write("admin+add+" + name);
                }
                else
                {
                    admins.RemoveAt(admins.IndexOf(whom.nick));
                    whom.Write("admin+del+" + name);
                }
            }
        }
        public void Admin(User u, bool set)
        {
            if (u == null) return;
            if (users.IndexOf(u.nick) == -1) return;
            if ((admins.IndexOf(u.nick) == -1) == set)
            {
                if (set)
                {
                    admins.Add(u.nick);
                    u.Write("admin+add+" + name);
                }
                else
                {
                    admins.RemoveAt(admins.IndexOf(u.nick));
                    u.Write("admin+del+" + name);
                }
            }
        }
        public void SetFirstAdmin(User u)
        {
            admins.Add(u.nick);
            u.Write("admin+add+" + name);
        }
        public bool IsUserAdmin(User u)
        {
            return admins.IndexOf(u.nick) != -1;
        }
    }
    public static class BrowserBehavior
    {
        public static readonly DependencyProperty HtmlProperty = DependencyProperty.RegisterAttached(
            "Html",
            typeof(string),
            typeof(BrowserBehavior),
            new FrameworkPropertyMetadata(OnHtmlChanged));

        [AttachedPropertyBrowsableForType(typeof(WebBrowser))]
        public static string GetHtml(WebBrowser d)
        {
            return (string)d.GetValue(HtmlProperty);
        }

        public static void SetHtml(WebBrowser d, string value)
        {
            d.SetValue(HtmlProperty, value);
        }

        public static void AddHtml(WebBrowser d, string value)
        {
            d.SetValue(HtmlProperty, GetHtml(d) + value);
        }

        static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            WebBrowser wb = d as WebBrowser;
            if (wb != null)
            {
                wb.NavigateToString(e.NewValue as string);
            }
        }
    }
    public static class ChatMessageFilter
    {
        public static string SendComment(string message)
        {
            return "<div class=\"comment\">"+message+"</div>";
        }
        public static string SendText(string user, string message)
        {
            return "<div class=\"message user_"+user+"\"><span class=\"user\">"+user+": </span><span class=\"content\">"+message+"</span></div>";
        }
        public static string AdminSendText(string message)
        {
            return "<div class=\"message admin\"><span class=\"user\">Serwer: </span><span class=\"content\">" + message + "</span></div>";
        }
    }
    public class ColorPackage
    {
        public Color darkColor { get; private set; }
        public Color color3 { get; private set; }
        public Color lightColor { get; private set; }
        public Color disableColor { get; private set; }
        public Color color3Selected { get; private set; }
        public Color color3Shadow { get; private set; }
        public Color color3SelectedShadow { get; private set; }
        public ColorPackage(Color dark, Color light, Color dis, Color c3, Color c3sel, Color c3sha, Color c3selsha)
        {
            darkColor = dark;
            lightColor = light;
            disableColor = dis;
            color3 = c3;
            color3Selected = c3sel;
            color3Shadow = c3sha;
            color3SelectedShadow = c3selsha;
        }
        public void Set(MainWindow main)
        {
            main.Resources["DarkBrush"] = new SolidColorBrush(darkColor);
            main.Resources["LightBrush"] = new SolidColorBrush(lightColor);
            main.Resources["DisableBrush"] = new SolidColorBrush(disableColor);
            main.Resources["3Color"] = new SolidColorBrush(color3);
            main.Resources["3ColorSelected"] = new SolidColorBrush(color3Selected);
            main.Resources["3ColorShadow"] = color3Shadow;
            main.Resources["3ColorSelectedShadow"] = color3SelectedShadow;
            main.Background = new SolidColorBrush(darkColor);
        }
    }
    public static class ColorsManager
    {
        public static List<ColorPackage> packages;
        public static ColorPackage currentPackage;
        public static Color FromStr(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        public static void Load()
        {
            packages = new List<ColorPackage>();
            packages.Add(new ColorPackage(FromStr("#FF8522BD"), FromStr("#FFA46FE4"), FromStr("#FFD0C0E2"), FromStr("#FF318EE0"), FromStr("#FF48A8FF"), FromStr("#FF1F73BD"), FromStr("#FF339FFF")));
            packages.Add(new ColorPackage(FromStr("#FF005194"), FromStr("#FF007BE0"), FromStr("#FF75C3FF"), FromStr("#FF945A00"), FromStr("#FFE08800"), FromStr("#FF774700"), FromStr("#FFB56C00")));
            packages.Add(new ColorPackage(FromStr("#FFEB0A00"), FromStr("#FFFF005E"), FromStr("#FFFF84B3"), FromStr("#FFB0009F"), FromStr("#FFC40DFF"), FromStr("#FF7A0070"), FromStr("#FF8A0AB5")));
            packages.Add(new ColorPackage(FromStr("#FF5E5E5E"), FromStr("#FF858585"), FromStr("#FFC4C4C4"), FromStr("#FF777777"), FromStr("#FF858585"), FromStr("#FF606060"), FromStr("#FF515151")));
            currentPackage = packages[0];
        }
        public static void Set(int index, MainWindow main)
        {
            if (index < 0 || index >= packages.Count) return;
            currentPackage = packages[index];
            currentPackage.Set(main);
            main.UpdateChatsColors();
        }
        public static void GenerateColorsLayout(MainWindow main)
        {
            for (int i = 0; i < packages.Count; i++)
            {
                Border border = new Border();
                Grid mainGrid = new Grid();
                mainGrid.Margin = new Thickness(1, 1, 1, 1);
                Grid top = new Grid();
                top.Margin = new Thickness(0, 0, 0, 24);
                top.Background = new SolidColorBrush(packages[i].darkColor);
                Grid left = new Grid();
                left.Margin = new Thickness(0, 24, 24, 0);
                left.Background = new SolidColorBrush(packages[i].color3);
                Grid right = new Grid();
                right.Margin = new Thickness(24, 24, 0, 0);
                right.Background = new SolidColorBrush(packages[i].lightColor);
                mainGrid.Children.Add(top);
                mainGrid.Children.Add(left);
                mainGrid.Children.Add(right);
                border.Child = mainGrid;
                border.Tag = i;
                border.MouseDown += delegate
                {
                    Set((int)border.Tag, main);
                };
                main.SettingsColorsPanel.Children.Add(border);
            }
        }
    }
}
//before += ".user_"+USER_NICK+" .content{border:1px solid rgb(133,34,189);background-color:rgb(164,111,228);color:white;}";
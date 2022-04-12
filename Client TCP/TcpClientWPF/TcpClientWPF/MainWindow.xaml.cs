using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using mshtml;

namespace TcpClientWPF
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region variables
        private TcpClient client;
        private BinaryReader reading = null;
        private BinaryWriter writing = null;
        private bool activeCall = false;
        private readonly BackgroundWorker bwConnection;
        private readonly BackgroundWorker bwMessages;
        private string nick;
        List <Chat> chats;
        Chat selectedChat;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            ColorsManager.Load();
            ColorsManager.GenerateColorsLayout(this);
            wbMessages.LoadCompleted += delegate
            {
                HTMLDocument html = (HTMLDocument)wbMessages.Document;
                html.parentWindow.scroll(0, 100000000);
            };
            chats = new List<Chat>();
            HideGrids(); // Ukrywanie wszystkich zakładek
            GridConnection.Visibility = Visibility.Visible; // Włączenie zakładki z połączeniami
            update(false);
            // Ładowanie ikonek - trzeba neistety to robić dynamicznie -_-
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
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "img/settings.png"))
            {
                Uri uri = new Uri(AppDomain.CurrentDomain.BaseDirectory + "img/settings.png", UriKind.RelativeOrAbsolute);
                ImageSettings.Source = BitmapFrame.Create(uri);
            }
            // selectedChat = new Chat(wbMessages);
            

            bwConnection = new BackgroundWorker();
            bwConnection.DoWork += delegate {
                connect();
            };
            bwMessages = new BackgroundWorker();
            bwMessages.DoWork += delegate
            {
                message();
            };
        }

        #region functions
        public void UpdateChatsColors()
        {
            for (int i = 0; i < chats.Count; i++)
            {
                chats[i].UpdateColors();
            }
        }

        void UpdateListChats()
        {
            lbChats.Items.Clear();
            for (int i = 0; i < chats.Count; i++)
            {
                lbChats.Items.Add(chats[i].name);
            }
        }

        void SelectChat(int index)
        {
            bool chose = index >= 0;
            bAddNickToChat.IsEnabled = chose;
            bRemoveNickFromChat.IsEnabled = chose;
            bLeaveChat.IsEnabled = chose;
            bDeleteChat.IsEnabled = chose;
            bSend.IsEnabled = chose;
            lbChats.SelectedIndex = index;
            if (index < 0)
            {
                selectedChat = null;
                BrowserBehavior.SetHtml(wbMessages, " ");
            }
            else
            {
                selectedChat = chats[index];
                selectedChat.Show();
            }
        }

        void updatePanelAdmin(bool admin)
        {
            switch(admin)
            {
                case true:
                    bDeleteChat.IsEnabled = true;
                    bAddPermission.IsEnabled = true;
                    bRemovePermission.IsEnabled = true;
                    break;

                case false:
                    bDeleteChat.IsEnabled = false;
                    bAddPermission.IsEnabled = false;
                    bRemovePermission.IsEnabled = false;
                    break;
            }
        }

        string Replace(string old, string newS, string text)
        {
            return string.Join(newS, text.Split(new string[] { old }, StringSplitOptions.None));
        }
        #endregion
        #region connection
        private void connect()
        {
            string host = "";
            int port = 0;
            Dispatcher.Invoke(new Action(delegate {
                host = tbHostAddress.Text;
                port = System.Convert.ToInt16(1410);
            }));

            
            // uruchomienie bw -> nazwa.runWorkerAsync();
            // kończenie pracy bw -> nazwa.cancelAsync();

            try
            {
                client = new TcpClient(host, port);
                NetworkStream ns = client.GetStream();
                reading = new BinaryReader(ns);
                writing = new BinaryWriter(ns);
                writing.Write(nick);

                string com = reading.ReadString();
                if (com == "OK")
                {
                    activeCall = true;
                    update(true);
                    bwMessages.RunWorkerAsync();

                    Dispatcher.Invoke(new Action(() => {
                        lbMessage.Items.Add("Nawiązano połączenie z " + host + " na porcie " + port);
                        chats.Clear();
                        tbNick.IsEnabled = false;
                        bSend.IsEnabled = true;
                        bDisconnect.IsEnabled = true;
                        tblNick.Text = tbNick.Text;
                    }));
                }
                else
                {
                    Dispatcher.Invoke(new Action(() => {
                        lbMessage.Items.Add(com);
                    }));
                    activeCall = false;
                    update(false);
                    client.Close();
                    bConnect.IsEnabled = true;
                }
              
            }
            catch (Exception ex)
            {
                activeCall = false;
                update(false);
                
                Dispatcher.Invoke(new Action(() => {
                    lbMessage.Items.Add("Błąd: Nie udało się nawiązać połączenia.");
                    bConnect.IsEnabled = true;
                    bDisconnect.IsEnabled = false;
                    bConnect.Content = "Połącz";
                }));

                //MessageBox.Show(ex.ToString(), "Błąd");
            }
        }
        #endregion
        #region message
        private void message()
        {
            try
            {
                // Console.WriteLine("Listen...");
                string messageReceived;
                while ((messageReceived = reading.ReadString()) != "END")
                {
                    Dispatcher.Invoke(new Action(delegate
                    {
                        string[] command = messageReceived.Split('+');
                        switch (command[0])
                        {
                            case "add":

                                Console.WriteLine("Create new chat");
                                
                                Chat newChat = new Chat(nick, command[1], wbMessages);
                                chats.Add(newChat);
                                lbChats.Items.Add(command[1]);
                                tbChatName.Text = "";
                                writing.Write("full+" + newChat.name);
                                
                                break;

                            case "write":

                                Console.WriteLine("Read message chat " + command[1]);
                                string mess = messageReceived.Substring(command[0].Length + command[1].Length + 2);
                                chats[getIndex(command[1])].Add(mess);

                                if (selectedChat != null) selectedChat.Show();

                                break;

                            case "del":

                                // usunięcie chatu

                                string selChatName = "+";
                                if (selectedChat != null) selChatName = selectedChat.name;
                                Console.WriteLine("Delete chat " + command[1]);
                                int index = getIndex(command[1]);
                                if (index != -1)
                                {
                                    chats.RemoveAt(index);
                                    lbChats.Items.RemoveAt(index);
                                }
                                int index2 = getIndex(selChatName);
                                SelectChat(index2);

                                break;

                            case "full":

                                // pobranie całego kontekstu z chatu

                                string mess2 = messageReceived.Substring(command[0].Length + command[1].Length + 2);
                                Console.WriteLine("Load full context" + mess2);

                                chats[getIndex(command[1])].Clear();
                                chats[getIndex(command[1])].Add(mess2);

                                break;

                            case "admin":
                                Console.WriteLine("Add admin to user");
                                switch(command[1])
                                {
                                    case "add":
                                        chats[getIndex(command[2])].isAdmin = true;
                                        if (selectedChat != null) updatePanelAdmin(true);
                                        break;
                                    case "del":
                                        chats[getIndex(command[2])].isAdmin = false;
                                        if (selectedChat != null) updatePanelAdmin(false);
                                        break;
                                }
                                // admin+(add|del)+nazwa czatu
                                break;
                        }
                        Console.WriteLine(messageReceived);
                    }));
                    
                }

                Dispatcher.Invoke(new Action(delegate {
                    lbMessage.Items.Add("Serwer zakończył połączenie");
                    activeCall = false;
                    update(false);
                    bConnect.IsEnabled = true;
                    tbNick.IsEnabled = true;
                    bDisconnect.IsEnabled = false;
                    bSend.IsEnabled = false;
                    HideGrids();
                    GridConnection.Visibility = Visibility.Visible;
                }));
            }
            catch
            {
                Dispatcher.Invoke(new Action(delegate {
                    lbMessage.Items.Add("Serwer zakończył połączenie");
                    activeCall = false;
                    update(false);
                    bConnect.IsEnabled = true;
                    tbNick.IsEnabled = true;
                    bDisconnect.IsEnabled = false;
                    bSend.IsEnabled = false;
                    HideGrids();
                    GridConnection.Visibility = Visibility.Visible;
                }));
            }

        }

        // przeszukanie listy chatów nazwa -> index
        private int getIndex(string name)
        {
            int index = -1;
            for (int i = 0; i < chats.Count; i++)
            {
                if (chats[i].name == name) index = i;
            }
            return index;
        }

        #endregion
        #region buttons
        private void PanelButtonMinimalize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        private void BConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bConnect.IsEnabled = false;
                // bConnect.Content = "Łączenie";
                bConnect.IsEnabled = false;
                nick = tbNick.Text;
                update(false);
                bwConnection.RunWorkerAsync();
            }
            catch { }
        }


        private void bDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                bwConnection.CancelAsync();
            }
            catch { }

            try
            {
                activeCall = false;
                update(false);
                client.Close();

                lbMessage.Items.Add("Rozłączono");
                // lbMessage.TopIndex = lbMessage.Items.Count - 1;
                bConnect.IsEnabled = true;
                bDisconnect.IsEnabled = false;

            }
            catch
            {
                lbMessage.Items.Add("Błąd rozłączania!");
                //lbMessage.TopIndex = lbMessage.Items.Count - 1;
            }
        }

        #endregion

        // --------------------------------------------------
        
        private void sendMessage()
        {
            if (!activeCall){
                Console.WriteLine("False");
                return;
            }
            if (tbMessage.Text == "END")
            {
                activeCall = false;
                update(false);
                client.Close();
                lbMessage.Items.Add("Zakończono pracę klienta");
                bConnect.IsEnabled = true;
                bDisconnect.IsEnabled = false;
            }
            else
            {
       
                string text = tbMessage.Text;
                text = Replace("haha", "<img src='http://teraz.com.pl/vardata/gify/emoty/emoty075.gif'/>", text);
                text = Replace(";)", "<img src='https://m.salon24.pl/2edd0c911fbbeed9e59d7cec9937ecb7,750,0,0,0.png'/>", text);
                text = Replace(";*", "<img src='https://m.salon24.pl/ffd729110410b5e218f37205ab8de149,750,0,0,0.png'/>", text);
                text = Replace(":O", "<img src='https://m.salon24.pl/aec386931da5acda580a73a8cfc13e21,750,0,0,0.png'/>", text);
                text = Replace(":3", "<img src='https://m.salon24.pl/d81025bab8bf711d4949d19d7fd7e145,750,0,0,0.png'/>", text);
                text = Replace(":>", "<img src='https://m.salon24.pl/30209d50a6994d55e4c4e6cd7aee2c9b,750,0,0,0.png'/>", text);
                text = Replace(":D", "<img src='https://m.salon24.pl/7863d30d3070c08414bcddb8f358b301,750,0,0,0.png'/>", text);
             


                if (chats.Count > 0)
                    writing.Write("write+" + selectedChat.name + "+" + text);
            }
            // Write("Me> "+tbMessage.Text);

            tbMessage.Text = "";
            //lbMessages.TopIndex = lbMessages.Items.Count - 1;
            tbMessage.Focus();
        }

        private void bSend_Click_1(object sender, RoutedEventArgs e)
        {
            sendMessage();
        }

        private void tbMessage_KeyDown_1(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && selectedChat != null)
                sendMessage();
        }

        void Write(string s)
        {
            selectedChat.Add("<div>" + s + "</div>");
            selectedChat.Show();
        }

        string addAction(int pos, string tekst, string action)
        {
            string tmp = "";
            for (int i = 0; i < pos; i++)
            {
                tmp += tekst[i];
            }
            tmp += action;
            for (int i = pos; i < tekst.Length; i++)
            {
                tmp += tekst[i];
            }
            return tmp;
        }

        // Zamykanie aplikacji
        private void PanelButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Przesuwanie aplikacji
        private void Panel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch { }
        }

        // Ukrywanie wszystkich okienek
        void HideGrids()
        {
            GridChat.Visibility = Visibility.Hidden;
            GridConnection.Visibility = Visibility.Hidden;
            GridChat.Visibility = Visibility.Hidden;
            GridSettings.Visibility = Visibility.Hidden;
        }

        // Pokazywanie tylko wybranego okienka po wciśnięciu w lewym panelu
        private void ButtonConnection_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideGrids();
            GridConnection.Visibility = Visibility.Visible;
        }

        private void ButtonChat_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideGrids();
            //GridChats.Visibility = Visibility.Visible;
            GridChat.Visibility = Visibility.Visible;
            UpdateListChats();
            SelectChat(-1);
        }

        private void ButtonSettings_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideGrids();
            GridSettings.Visibility = Visibility.Visible;
        }

        private void BAddChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                writing.Write("new+" + tbChatName.Text);
            }
            catch
            {
                lbMessage.Items.Add("Podczas dodawania chatu wystąpił błąd");
                HideGrids();
                GridConnection.Visibility = Visibility.Visible;
            }

        }

        private void BAddNickToChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                writing.Write("add+" + selectedChat.name + "+" + tbNickToChat.Text);
                writing.Write("full+" + selectedChat.name);
                tbNickToChat.Text = "";
            }
            catch
            {
                lbMessage.Items.Add("Podczas dodawania " + tbNickToChat.Text + " wystąpił błąd");
                HideGrids();
                GridConnection.Visibility = Visibility.Visible;
            }
        }

        private void update(bool activeCall)
        {
            Dispatcher.Invoke(new Action(delegate
            {
                if (activeCall)
                {
                    tbNick.IsEnabled = false;
                    GridChat.IsEnabled = true;

                    tbMessage.IsEnabled = true;
                    GridActions.IsEnabled = true;
                    bSend.IsEnabled = true;
 
                }
                else
                {
                    tbNick.IsEnabled = true;
                    GridChat.IsEnabled = false;
                    
                    tbMessage.IsEnabled = false;
                    GridActions.IsEnabled = false;
                    bSend.IsEnabled = false;

                    bDeleteChat.IsEnabled = false;
                    bAddPermission.IsEnabled = false;
                    bRemovePermission.IsEnabled = false;
                }
                
              }));

            if (selectedChat != null) updatePanelAdmin(selectedChat.isAdmin);
            }

        #region bold, italic, underline buttons
        private void bBold_Click(object sender, RoutedEventArgs e)
        {
            string tekst = addAction(tbMessage.CaretIndex, tbMessage.Text, "<b></b>");
            int currentPos = tbMessage.CaretIndex;
            tbMessage.Focus();
            tbMessage.Text = tekst;
            tbMessage.CaretIndex = currentPos + 3;
        }
        private void bItalic_Click(object sender, RoutedEventArgs e)
        {
            string tekst = addAction(tbMessage.CaretIndex, tbMessage.Text, "<i></i>");
            int currentPos = tbMessage.CaretIndex;
            tbMessage.Focus();
            tbMessage.Text = tekst;
            tbMessage.CaretIndex = currentPos + 3;
        }
        private void bUnderline_Click(object sender, RoutedEventArgs e)
        {
            string tekst = addAction(tbMessage.CaretIndex, tbMessage.Text, "<u></u>");
            int currentPos = tbMessage.CaretIndex;
            tbMessage.Focus();
            tbMessage.Text = tekst;
            tbMessage.CaretIndex = currentPos + 3;
        }
        #endregion

        private void LbChats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectChat(lbChats.SelectedIndex);
            if (selectedChat != null) updatePanelAdmin(selectedChat.isAdmin);
            else updatePanelAdmin(false);
        }

        private void TbChatName_TextChanged(object sender, TextChangedEventArgs e)
        {
            int pos = tbChatName.Text.IndexOf('+');
            if (pos != -1)
            {
                tbChatName.Background = new SolidColorBrush(Color.FromRgb(255, 128, 128));
                bAddChat.IsEnabled = false;
            }
            else
            {
                tbChatName.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                bAddChat.IsEnabled = true;
            }
        }

        private void BLeaveChat_Click(object sender, RoutedEventArgs e)
        {
            writing.Write("exit+" + selectedChat.name);
        }

        private void BDeleteChat_Click(object sender, RoutedEventArgs e)
        {
            writing.Write("del+" + selectedChat.name);
        }

        private void BRemoveNickFromChat_Click(object sender, RoutedEventArgs e)
        {
            writing.Write("rem+" + selectedChat.name + "+" + tbNickToChat.Text);
            tbNickToChat.Text = "";
        }

        private void BAddPermission_Click(object sender, RoutedEventArgs e)
        {
            writing.Write("admin+add+" + tbNickToChat.Text + "+" + selectedChat.name);
            tbNickToChat.Text = "";
        }

        private void BRemovePermission_Click(object sender, RoutedEventArgs e)
        {
            writing.Write("admin+del+" + tbNickToChat.Text + "+" + selectedChat.name);
            tbNickToChat.Text = "";
        }
    }

    public class Chat
    {
        string before;
        string after;
        string content;
        WebBrowser wb;
        public string nick { get; private set; }
        public string name { get; private set; }
        public bool isAdmin;

        public Chat(string _nick, string _name, WebBrowser webBrowser)
        {
            isAdmin = false;
            try
            {
                name = _name;
                nick = _nick;
                wb = webBrowser;
                content = "";
                /*before = "<!DOCTYPE html><html><head><meta charset =\'UTF-8\'/><style>" +
                    "*{font-family:Consolas,monospace;font-size:16px;}.comment{color:#aaaaaa;text-align:center;}.message{padding:2px 6px;}" +
                    ".message .content{border:1px solid #aaaaaa;border-radius:7px;padding:10px;}.message.admin .user{font-weight:bold;color:rgb(133,34,189);}" +
                    ".message.admin .content{border:2px solid rgb(100,26,140);background:rgb(133,34,189);color:white;}span{display:block;}" +
                    ".user_" + nick + " .content{ border: 1px solid rgb(133, 34, 189); background: rgb(164, 111, 228); color: white; } .content{overflow: hidden;}" +
                    "</style></head><body>";
                /*before = "<!DOCTYPE html><html><head><meta charset=\"UTF-8\"/><style>*{font-family: Consolas, monospace; font-size: 12px;}" +
                    ".comment{color: #aaaaaa; text-align: center;} .message{padding: 10px;} .message .content{border: 1px solid #aaa; border-radius: 7px; padding: 10px;}" +
                    ".message.admin .user {font-weight: bold; color: rgb(133, 34, 189);}" +
                    ".message.admin .content{border: 2px solid rgb(100, 26, 140); background: rgb(133, 34, 189); color: #fff;}" +
                    "span{display: block;} .user_" + nick + " .content{border: 1px solid rgb(13, 34, 189); background: rgb(200, 180, 241);}</style></head><body>";*/
                after = "</body></html>";
                UpdateColors();
                //BrowserBehavior.SetHtml(wb, before + after);
            }
            catch
            {
                Console.WriteLine("Error in webBrowser");
            }
        }
        public void UpdateColors()
        {
            ColorPackage p = ColorsManager.currentPackage;
            before = "<!DOCTYPE html><html><head><meta charset=\"UTF-8\"/><style>*{font-family:Consolas,monospace;font-size:16px;}.comment{color:#aaaaaa;text-align:center;}.message{padding:2px 6px;}" +
                ".message .content{border:1px solid #aaaaaa;border-radius:7px;padding:10px;overflow:hidden;}.message.admin .user{font-weight:bold;color:rgb(" + p.darkColor.R + "," + p.darkColor.G + "," + p.darkColor.B + ");}" +
                "img{height: 20px;} .message.admin .content{border:2px solid rgb(" + p.darkColor.R + "," + p.darkColor.G + "," + p.darkColor.B + ");background:rgb(" + p.lightColor.R + "," + p.lightColor.G + "," + p.lightColor.B + ");color:white;}span{display:block;}" +
                ".user_" + nick + " .content{ border: 1px solid rgb(" + p.color3.R + "," + p.color3.G + "," + p.color3.B + "); background: rgb(" + p.color3Selected.R + "," + p.color3Selected.G + "," + p.color3Selected.B + "); color: white; } .content{overflow: hidden;}" + "</style></head><body>";
        }
        public void Add(string s)
        {
            content += s;
        }
        public void Clear()
        {
            content = "";
        }
        public void Show()
        {
            BrowserBehavior.SetHtml(wb, before + content + after);
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
            HTMLDocument doc = (HTMLDocument)d.Document;
            doc.parentWindow.scroll(0, 9999);
        }

        public static void AddHtml(WebBrowser d, string value)
        {
            d.SetValue(HtmlProperty, GetHtml(d) + value);
        }

        static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            WebBrowser wb = d as WebBrowser;
            if (wb != null)
                wb.NavigateToString(e.NewValue as string);
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

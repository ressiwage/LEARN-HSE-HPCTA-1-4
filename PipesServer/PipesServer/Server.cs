using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;



namespace Pipes
{
    public partial class frmMain : Form
    {
        private Int32 PipeHandle;                                                       // дескриптор канала
        private string PipeName = "\\\\" + Dns.GetHostName() + "\\pipe\\ServerPipe";    // имя канала, Dns.GetHostName() - метод, возвращающий имя машины, на которой запущено приложение
        private Thread t;                                                               // поток для обслуживания канала
        private volatile bool _continue = true;                                                  // флаг, указывающий продолжается ли работа с каналом
        private List<string> clients = new List<string>(); 

        public static bool IsBasicLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
        public static bool FormatValid(string format)
        {

            foreach (char c in format)
            {
                // This is using String.Contains for .NET 2 compat.,
                //   hence the requirement for ToString()
                if (!IsBasicLetter(c))
                    return false;
            }

            return true;
        }
        public static String getPipeValidName() {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "введите никнейм",
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 50, Top=20, Text="введите никнейм", Width=400 };
            TextBox textBox = new TextBox() { Left = 50, Top=50, Width=400 };
            Button confirmation = new Button() { Text = "Ok", Left=350, Width=100, Top=70 };
            confirmation.Click += (sender, e) => {
                if (FormatValid(textBox.Text))
                {
                    prompt.Close();
                }
                else
                {
                    textLabel.Text = "Некорректный ввод. Никнейм должен содержать только латинские буквы";
                    textLabel.ForeColor = Color.Red;
                }
            };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.ShowDialog();
            String nickname = textBox.Text == "" ? "anon" : textBox.Text;
            return nickname;
        }
        public uint SendToPipe(string message, string pipe)
        {
            uint BytesWritten = 0;  // количество реально записанных в канал байт
            byte[] buff = Encoding.Unicode.GetBytes(
                message
                );    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            // открываем именованный канал, имя которого указано в поле tbPipe
            Int32 PipeHandle = DIS.Import.CreateFile(pipe, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
            DIS.Import.WriteFile(PipeHandle, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал
            DIS.Import.CloseHandle(PipeHandle);
            return BytesWritten;
        }
        // конструктор формы
        public frmMain()
        {

            InitializeComponent();
            //на будущее: нужно будет сделать так: в сообщение зашит получатель и отправитель. Сервер получает сообщение 
            // по пайпу server и пытается открыть пайп-никнейм. если не получилось, возвращает ответ "не получилось", иначе возвращает ок.
            //   также клиент открывает у себя 2 пайпа: пайп для сервера и именованный пайп для получения сообщений.

            //новый план: в треде 
            // создание именованного канала
            PipeHandle = DIS.Import.CreateNamedPipe("\\\\.\\pipe\\ServerPipe", DIS.Types.PIPE_ACCESS_DUPLEX, DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT, DIS.Types.PIPE_UNLIMITED_INSTANCES, 0, 1024, DIS.Types.NMPWAIT_WAIT_FOREVER, (uint)0);

            // вывод имени канала в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + PipeName;
            
            // создание потока, отвечающего за работу с каналом
            t = new Thread(ReceiveMessage);
            t.Start();
        }

        private void ReceiveMessage()
        {
            string msg = "";            // прочитанное сообщение
            uint realBytesReaded = 0;   // количество реально прочитанных из канала байтов

            // входим в бесконечный цикл работы с каналом
            while (_continue)
            {
                if (DIS.Import.ConnectNamedPipe(PipeHandle, 0))
                {
                    byte[] buff = new byte[1024];                                           // буфер прочитанных из канала байтов
                    DIS.Import.FlushFileBuffers(PipeHandle);                                // "принудительная" запись данных, расположенные в буфере операционной системы, в файл именованного канала
                    DIS.Import.ReadFile(PipeHandle, buff, 1024, ref realBytesReaded, 0);    // считываем последовательность байтов из канала в буфер buff
                    msg = Encoding.Unicode.GetString(buff);                                 // выполняем преобразование байтов в последовательность символов
                    Console.WriteLine(msg);
                    rtbMessages.Invoke((MethodInvoker)delegate
                    {
                        if (msg != "")
                        {
                            string[] data = msg.Split(new string[] { " <:> " }, StringSplitOptions.None);
                            string clientpipename = "\\\\"+data[0]+"\\pipe\\"+data[1];
                            if (!clients.Contains(clientpipename))
                            {
                                clients.Add(clientpipename);
                                rtbParticipants.Text += data[1] + "\n";
                            }
                            DateTime dt = DateTime.Now;
                            string time = dt.Hour + ":" + dt.Minute+":"+dt.Second;

                            string message = "\n >> "  + data[1] + "|" + data[0] + "|" + time  + ":  " + data[2];                             // выводим полученное сообщение на форму
                            rtbMessages.Text += message;
                            foreach (string pipe in clients)
                            {
                                Console.WriteLine(message+" "+pipe);
                                SendToPipe(message, pipe);
                            }

                        }
                            
                    });

                    DIS.Import.DisconnectNamedPipe(PipeHandle);                             // отключаемся от канала клиента 
                    Thread.Sleep(500);                                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с каналом завершена

            if (t != null)
                t.Abort();          // завершаем поток
            
            if (PipeHandle != -1)
                DIS.Import.CloseHandle(PipeHandle);     // закрываем дескриптор канала
        }
    }
}
using System;                           //程序主体 需要
using System.Collections.Generic;       //List 需要
using System.Diagnostics;               //Process 需要
using System.Runtime.InteropServices;   //DllImport 需要    
using System.Text;                      //StringBuilder 需要
using System.Windows.Forms;             //msgbox 需要
using System.Xml;                       //读XML

namespace HideWhenMinimize
{
    static class Program
    {

        #region  -- PreHook & DllImport --

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
                IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        //[DllImport("user32.dll", CharSet = CharSet.Unicode)]
        //private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lclassName, string windowTitle);

        private delegate bool WNDENUMPROC(IntPtr hWnd, int lParam);
        //用来遍历所有窗口 
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, int lParam);

        //获取窗口Text 
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)]StringBuilder lpString, int nMaxCount);

        //获取窗口类名 
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)]StringBuilder lpString, int nMaxCount);

        //获取hWnd所在pid
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        //自定义一个类，用来保存句柄信息，在遍历的时候，随便也用空上句柄来获取些信息，呵呵 
        public struct WindowInfo
        {
            public IntPtr hWnd;
            public string szWindowName;
            public string szClassName;
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        #endregion


        #region -- Message Loop [Not In Used]--
        /*
        [SerializableAttribute]
        public struct MSG
        {
            public IntPtr hwnd;
            public IntPtr lParam;
            public UInt32 message;
            public UInt32 pt_x;
            public UInt32 pt_y;
            public UInt32 time;
            public IntPtr wParam;
        }

        [DllImport("user32.dll")]
        static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
        [DllImport("user32.dll")]
        static extern bool TranslateMessage([In] ref MSG lpMsg);
        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage([In] ref MSG lpmsg);
        */
        #endregion


        #region -- [Functions] --

        //return -1=not run     -2=exeName is null
        private static int ProcessIfExist(string exeName)
        {
            //不能处理包含命令行参数的进程
            if (exeName == null) return -2;
            if (exeName.Contains(@"\")) exeName = exeName.Remove(0, exeName.LastIndexOf(@"\") + 1);
            if (exeName.EndsWith(".exe", true, null)) exeName = exeName.Remove(exeName.Length - 4);

            foreach (Process p in Process.GetProcesses())
                if (p.ProcessName == exeName)
                    return p.Id;
            return -1;
        }


        private static string readXML(string XMLPath, string nodePath, string errorOutPut = "")
        {
            if (System.IO.File.Exists(XMLPath) == false) return errorOutPut;

            try {
                XmlDocument doc = new XmlDocument();
                doc.Load(XMLPath);
                XmlElement root = null;
                root = doc.DocumentElement;
                XmlNodeList listNodes = null;
                listNodes = root.SelectNodes(nodePath);      //  "/root1/root2/root3"
                foreach (XmlNode node in listNodes)
                    return node.InnerText;
                return errorOutPut;
            } catch {
                return errorOutPut;
            }
        }


        private static WindowInfo[] GethWndInfoFromPID(int pid)
        {

            //用来保存窗口对象 列表
            List<WindowInfo> wndList = new List<WindowInfo>();

            //enum all desktop windows 
            EnumWindows(delegate(IntPtr hWnd, int lParam)
            {
                uint lpdwProcessId;
                GetWindowThreadProcessId(hWnd, out lpdwProcessId);

                if (lpdwProcessId == pid) {
                    WindowInfo wnd = new WindowInfo();
                    StringBuilder sb = new StringBuilder(128);

                    //get hwnd 
                    wnd.hWnd = hWnd;

                    //get window name  
                    GetWindowText(hWnd, sb, sb.Capacity);
                    if (sb.ToString() == string.Empty) return true;     //discard if name is empty
                    wnd.szWindowName = sb.ToString();

                    //get window class 
                    GetClassName(hWnd, sb, sb.Capacity);
                    wnd.szClassName = sb.ToString();

                    //add it into list 
                    wndList.Add(wnd);
                }
                return true;
            }, 0);

            return wndList.ToArray();
        }

        private static bool KillOtherDuplicate(bool ShowErrorMsg = false)
        {
            try {
                Process myP = Process.GetCurrentProcess();
                foreach (Process ptmp in Process.GetProcessesByName(myP.ProcessName))
                    if (myP.Id != ptmp.Id)
                        if (myP.MainModule.FileName == ptmp.MainModule.FileName)
                            ptmp.Kill();
                return true;
            } catch (Exception ex) {
                if (ShowErrorMsg)
                    MessageBox.Show("Error when killing duplicate listener.\r\nDetail:" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }


        private static IntPtr GetMainHwnd(int pid, int maxWaitMS, string hWndName, string hWndClass)
        {
            if (hWndName.Length == 0) return IntPtr.Zero;
            if (hWndClass.Length == 0 && maxWaitMS == 0) maxWaitMS = 12800;   //若MaxWait 与WinClass 都留空,则设置MaxWait为12.8秒
            if (maxWaitMS < 1000) maxWaitMS = 1000;  //reset to delay = 1000, if is x < 1000

            DateTime beginTime = DateTime.Now;
            while ((DateTime.Now - beginTime).TotalMilliseconds <= maxWaitMS) {
                //loop超过最长时间 则退出
                WindowInfo[] buffer = GethWndInfoFromPID(pid);

                foreach (WindowInfo i in buffer)
                    if (hWndClass.Length == 0) {    //没有hWndClass
                        if (i.szWindowName.Contains(hWndName))
                            return i.hWnd;
                    } else {                        //有hWndClass
                        if (i.szClassName == hWndClass && i.szWindowName.Contains(hWndName))
                            return i.hWnd;
                    }
            }
            return IntPtr.Zero;
        }

        #endregion


        /// <UpdateInfo>
        /// Ver 1.3.1.1219: 可以设为常驻监听进程(被监听程序退出后, 监听程序不退出, 持续搜索新的被监听程序)
        /// Ver 1.3.1.1207: 尝试解决休眠后监控程序自动退出的问题
        /// Ver 1.3.1.1202: 解决休眠后监控程序自动退出的问题
        /// Ver 1.3.0.1118: 极大优化了查找监视窗口的函数(降低了约1000倍的时间), 提升效率及有效性
        /// </UpdateInfo>
        #region $$ Main $$

        private static IntPtr mainHwnd = IntPtr.Zero;
        private static string exePath = null;

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string arg = null;
            if (args.Length > 0)
                arg = args[0];

            #region GetVarFromXML
            //string xmlPath = Process.GetCurrentProcess().ProcessName + ".xml";
            // System.Reflection.Assembly.GetExecutingAssembly().Location;  获取文件完整路径  极快
            string xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".xml";
            if (System.IO.File.Exists(xmlPath) == false) {
                MessageBox.Show("Setting(" + Process.GetCurrentProcess().ProcessName + ".xml) not found! Program will exit.",
                    "No Setting", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            //string exePath = null;        //  @"a\a" == "a\\a"
            exePath = readXML(xmlPath, "/Hook/Path");
            if (exePath == null) {
                MessageBox.Show("Target exe (path) is not available, please review in " + xmlPath + " (Node name is case sensitive).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string winName = null;
            winName = readXML(xmlPath, "/Hook/WinName");

            string winClass = null;
            winClass = readXML(xmlPath, "/Hook/WinClass");

            int maxWait = 0;
            int.TryParse(readXML(xmlPath, "/Hook/MaxWait"), out maxWait);

            bool infiniteLoop = false;
            string buffer = readXML(xmlPath, "/Hook/InfiniteLoop");
            if (buffer == "1" || buffer.ToLower() == "true")
                infiniteLoop = true;
            #endregion


            int pid = ProcessIfExist(exePath);

            bool alreadyRun;
            System.Threading.Mutex mtx =
                    new System.Threading.Mutex(false, exePath.Replace("\\", "|"), out alreadyRun);
            alreadyRun = !alreadyRun;

            if (alreadyRun) {
                //listener run && exe NOT run
                if (pid == -1)
                    //kill old listener
                    KillOtherDuplicate(true);
                else
                    //listener run && exe run
                    // -force close listener
                    if (arg == "-force")
                        //kill old listener
                        KillOtherDuplicate(true);
                    else
                        //listener run  &&  exe run
                        return;
            }

            int loopDelay = 0;
            do {
                //监听循环
                if (loopDelay > 0) {
                    System.Threading.Thread.Sleep(loopDelay);
                    pid = ProcessIfExist(exePath);
                    if (pid == -1)
                        continue;
                }

                //start new listener
                Process p;
                if (pid == -1) {
                    //start NEW exe and setup listener
                    p = new Process();
                    p.StartInfo.FileName = exePath;
                    if (arg == "-hide")
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                    p.Start();
                    p.WaitForInputIdle();
                } else {
                    //setup listener on OLD exe
                    p = Process.GetProcessById(pid);
                }
                //**listen process on eixt event***
                //p.EnableRaisingEvents = true;
                //p.Exited += new System.EventHandler(Process_Exited);
                mainHwnd = GetMainHwnd(p.Id, maxWait * 1000, winName, winClass);

                if (mainHwnd == IntPtr.Zero) {
                    //如果找不到目标窗口, 则尝试自动搜索
                    p.Refresh();
                    mainHwnd = p.MainWindowHandle;
                }
                if (IsIconic(mainHwnd)) ShowWindow(mainHwnd, 0);

                // Need to ensure delegate is not collected while we're using it,
                // storing it in a class field is simplest way to do this.
                WinEventDelegate procDelegate = new WinEventDelegate(WinEventProc);
                IntPtr hhook;

                // Listen for name change changes across all processes/threads on current desktop...
                hhook = SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZESTART,
                        IntPtr.Zero, procDelegate, (uint)p.Id, 0, WINEVENT_OUTOFCONTEXT);
                /* hhook = SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY,
                        IntPtr.Zero, procDelegate, (uint)p.Id, 0, WINEVENT_OUTOFCONTEXT);   */

                /* message loop
                MSG lpmsg;
                sbyte bRet = 0;
                while ((bRet = GetMessage(out lpmsg, IntPtr.Zero, 0, 0)) != 0 ) 
                    if (bRet == -1) {
                        // handle the error and possibly exit
                        break;
                    } else {
                        TranslateMessage(ref lpmsg);
                        DispatchMessage(ref lpmsg);
                    }
                 */

                //mre.Reset();
                ////wait until mre.set();
                //mre.WaitOne();

                try {
                    p.WaitForExit();
                } catch {
                    MessageBox.Show("<WaitForExit> Error.");
                }
                

                UnhookWinEvent(hhook);
                loopDelay = 888;
            } while (infiniteLoop);
            
            return;
        }
        #endregion


        #region -- Fire Event --
        private static void WinEventProc(IntPtr hWinEventHook, uint eventType,
                IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // filter out non-HWND namechanges... (eg. items within a listbox)
            if (idObject != 0 || idChild != 0) return;

            if (eventType == EVENT_SYSTEM_MINIMIZESTART && hwnd == mainHwnd)
                //hide window
                ShowWindow(hwnd, 0);
        }

        //private static System.Threading.ManualResetEvent mre = new System.Threading.ManualResetEvent(false);
        //private static void Process_Exited(object sender, EventArgs e)
        //{
        //    if (ProcessIfExist(exePath) == -1)  //if is not running
        //        //resume mre (let main exit)
        //        mre.Set();
        //}
        #endregion

    }
}

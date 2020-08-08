using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CommonTool;
using System.Threading;
using System.IO;
using HalconDotNet;
using HWindow;
using Nc.Mitsubishi;
using System.Windows.Forms.DataVisualization.Charting;
using System.Globalization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections;

namespace upperComputer
{
    #region Enum Struct
    //权限
    enum Limit
    {
        Null,
        Operator,
        Engineer
    }
    public enum MachineState
    {
        Run,
        Stop
    }

    public struct InspectResult
    {
        public int g_dXOff { get; set; }
        public int g_dYoff { get; set; }
        public int g_dangleoff { get; set; }
        public int bLoadRet { get; set; }


        //不良品分类：A级品为再公差范围内位、  B > 公差范围、   C < 公差范围    另外检测:是否有产品      
        public int isOk;
        public int okCount;
        public int ngCount;
        public HObject destImage;


        public string g_dAALength { get; set; }
        public string g_dBBLength { get; set; }
        public string g_dCCLength { get; set; }
        public string g_dDDLength { get; set; }
        public string Qrcode { get; set; }    //二维码

        public int Cam1ProcDone { get; set; }
        public int Cam2ProcDone { get; set; }

        public int Img1ProcHaveObj { get; set; }
        public int Img1ProcIsOk { get; set; }
        public int Img0ProcHaveObj { get; set; }
        public int Img0ProcIsOk { get; set; }
        public int channel { get; set; }
        public bool bWriteRecord { get; set; }              //是否已经被写记录
        public string strInspectResult { get; set; }
        public bool bCount { get; set; }
    }

    public struct AOIResult
    {

        //不良品分类：A级品为再公差范围内位、  B > 公差范围、   C < 公差范围    另外检测:是否有产品      
        public HObject destImage;

        public double g_dAALength { get; set; }
        public double g_dBBLength { get; set; }
        public double g_dCCLength { get; set; }
        public double g_dDDLength { get; set; }
        public string strCodeData { get; set; }    //二维码

        public bool Cam1ProcDone { get; set; }

        public bool Img1ProcHaveObj { get; set; }
        public int Img1ProcIsOk { get; set; }

        public int channel { get; set; }
        public bool bWriteRecord { get; set; }              //是否已经被写记录
        public string strInspectResult { get; set; }
        public bool bCount { get; set; }
    }

    public struct Roi
    {
        public string RoiName { get; set; }
        public double Angle { get; set; }
        public int CentreX { get; set; }
        public int CentreY { get; set; }
        public double Length1 { get; set; }
        public double Length2 { get; set; }
        public int Radius { get; set; }
        public int RoiType { get; set; }
        public int Row1 { get; set; }
        public int Row2 { get; set; }
        public int Column1 { get; set; }
        public int Column2 { get; set; }
    }

    public struct InspectParameters
    {
        public double DiaStd;
        public double DiaMinValue;
        public double DiaMaxValue;
    }

    #endregion

    public partial class FormMain : System.Windows.Forms.Form
    {

        #region Variable
        public static string ProgName { set; get; }
        public static string strProductName { set; get; }


        //INI路径
        public static string strAppPath = System.Windows.Forms.Application.StartupPath;   //获取exe路径
        public static string strComParIniPath = strAppPath + "\\CommunicationPar.ini";
        public static string strPrdctIParniPath = strAppPath + "\\ProductPar.ini";
        public static string strRbtIniPath = strAppPath + "\\RobotPar.ini";
        public static string CSVSavePath = "D\\Data";

        //子窗体
        public FormLogin FormLog = new FormLogin();
        public FormLimit FormLimit = new FormLimit();
        public FormProductParameter FormProdctPar = new FormProductParameter();
        public FormRobot FormRbt = new FormRobot();

        //委托
        public Action<string> UpdateShowInfo;

        public delegate void RefreshChartDelegate(List<double> A, List<double> B, List<double> C, List<double> D);

        public delegate void FormMainDelegate(string message, object data);
        public static event FormMainDelegate formMainDelegate;

        public delegate void MainDlgSendToProductDlg(string message, object data);
        public static event MainDlgSendToProductDlg mainDlgSendToProductDlg;


        public delegate void MainDlgSendToRobotDlg(string message, object data);
        public static event MainDlgSendToRobotDlg mainDlgSendToRobotDlg;

        public delegate void dInsertRunMsg(string message, bool data);
        public static event dInsertRunMsg InsertRunMsg;

        public delegate void MainDlgSendToLoginDlg(string message, string data);
        public static event MainDlgSendToLoginDlg mainDlgSendToLoginDlg;

        public delegate void MainDlgSendToLimitDlg(string message, string data);
        public static event MainDlgSendToLimitDlg mainDlgSendToLimitDlg;

        public static bool g_bCam0DispToPrductDlg = false;
        public static bool g_bCam1DispToPrductDlg = false;


        private WndRender FrmWndRender;     //窗口缩放到与屏幕尺寸适度

        private double ElspseTimeAOI;

        private DateTime StartTimeAOI;

        private TimeSpan SpanTimeAOI;

        private DateTime minValue;
        private DateTime maxValue;

        Stopwatch _sw = new Stopwatch();
        Stopwatch _sw2 = new Stopwatch();

        Stopwatch _single_GrabAOI = new Stopwatch();

        Stopwatch _single_AOIPP = new Stopwatch();


        public static string strErrorInfo = string.Empty;

        //***初始化标志（换型）
        public static int g_iChangeproduct = 0;

        //通信
        public static Plc PLC1;
        public static Plc PLC2;
        public static Plc PLC3;

        public static HTuple hv_CalibData;
        public static HTuple hv_CalibData1;
        public static HTuple hv_CalibData2;
        public static HTuple hv_CalibData3;
        public static HTuple hv_CalibData4;
        public static HTuple hv_CalibData5;
        public static HTuple hv_CalibData6;


        public static int g_iLimit = 0;
        public static int Cam0ImageIndex = 7;
        public static int Cam1ImageIndex = 4;

        public static int suckerIndex = 0; //吸嘴编号
        public static int LoadsuckerIndex = 0;
        public static string g_strOperaterName = "test";

        public static int g_iisNGBCount = 0;
        public static int g_iisNGCCount = 0;

        public static bool g_ImgGrabDone = true;            //图像处理完成
        public static int AOIInspectIndex = 1;            //AOI检测工位

        // public static bool g_bSendResultToRobot2 = false;   //发检测结果给2#机器人

        public static int InsertRunMsgCnt = 0;              //运行界面状态信息计数,条目每满999清除
        public static int g_iMachineState = 0;              //设备所处的状态

        public static bool g_bInitDevice = true;            //开机自动初始化设备
                                                            // MachineState MachineState = new MachineState();

        //线程      
        //Thread thRecvRobot1Data;    //接收1#机器人发来的数据
        //Thread thRecvRobot2Data;    //接收2#机器人发来的数据 
        //Thread thScanPlc1Data;      //接收1#PLC发来的数据 
        //Thread thRecvPlc2Data;      //接收2#PLC发来的数据 
        //Thread thRecvPlc3Data;      //接收3#PLC发来的数据 


        Thread thLoadCap;           //上料线程
        Thread thAOI;       //AOI线程
        Thread thScanMCD;          //扫描吸嘴号
        Thread thSaveImage;

        Thread thCountProcessResult;  //统计处理结果
        private Thread ThInitDevice;   //设备初始化

        //相机、图像
        public const int Cam0 = 0;
        public const int Cam1 = 1;

        public const int hCam0 = 0;
        public const int hCam1 = 1;

        public DoWithCamera cam0;
        public DoWithCamera cam1;


        MVSCam[] CamDevice = new MVSCam[2];

        private string[] g_strCamSerial = { "00E46504565", "00E48812464" };//   上料相机 00E46504565

        public static bool g_bCam0Open = false;     //相机打开成功状态
        public static bool g_bCam1Open = false;


        public static bool g_bGrabImg0 = false;        //拍取相机0  抓图完成
        public static bool g_bGrabImg1 = false;       //拍取相机1

        public static bool g_bProcessImg0 = false;    //处理相机0面光
        public static bool g_bProcessImg1 = false;    //处理相机1背光

        Thread thContinueGrabCam0;  //相机连续抓图线程
        Thread thContinueGrabCam1;

        public const uint HardTrigger = 0;
        public const uint SoftTrigger = 7;
        public static uint g_iAutoRunTriggerMode = HardTrigger;

        List<int> dataListg_iProdSeri = new List<int>() { };
        List<double> dataUP = new List<double>() { };
        List<double> dataLOW = new List<double>() { };

        List<double> dataListAA = new List<double>() { };
        List<double> dataListBB = new List<double>() { };
        List<double> dataListCC = new List<double>() { };
        List<double> dataListDD = new List<double>() { };


        public static SortedList<int, HObject> Cam1ImageList;
        public static SortedList<int, HObject> Cam0ImageList;

        public Queue<HObject> Cam0Imagequeue;
        public Queue<HObject> Cam1Imagequeue;

        //private SortedList<string, HObject> SaveNullImageList;
        //private SortedList<string, HObject> SaveOKImageList;
        //private SortedList<string, HObject> SaveNGImageList;
        //private SortedList<string, InspectResult> DestImageList;

        public const int SourceImage = 0;
        public const int OkImage = 1;
        public const int NgImage = 2;

        public static string g_strProdcutName = string.Empty;
        public static bool g_bCam0ContinueGrab = false;    //实时显示
        public static bool g_bCam1ContinueGrab = false;

        public static bool g_isSL_OnOff;//允许上料
        public static bool g_bZJisInLoadSite;//治具在上料位置
        public static bool g_bisFLWC;//放料完成

        public static HObject[] ho_SrcImg = new HObject[7];  //相机采集到的图像变量      
        public static HObject[] ho_RstImg = new HObject[4];  //处理结果图像变量
        public static SortedList<string, HObject> ImageListCam0;
        public static SortedList<string, HObject> ImageListCam1;


        HalconDotNet.HWindow[] hWin = new HalconDotNet.HWindow[8];


        public static HObject ho_LoadRoiCode = new HObject();
        public static HObject ho_LoadRoiMark = new HObject();

        public static HObject ho_AOIRoiCode = new HObject();
        public static HObject ho_AOIRoiMark = new HObject();

        public static HTuple QRCodeHandle = null;
        public static HTuple ECC200CodeHandle = null;


        public static HTuple DataCodeHandle = null;
        public static HTuple hv_CoordAngleOffset = null;
        public static HTuple modelModeCodeRow = null;
        public static HTuple ModeCodeCol = null;
        public static HTuple ModeCodeAngle = null;

        //结果统计
        public const int RecordLimit = 10000;
        public static InspectResult[] inspectResult = new InspectResult[RecordLimit];
        public static InspectParameters stInspPara = new InspectParameters();


        public static int g_iTotal = 0;                    //总计数
        public static int g_iOkCount = 0;                  //OK计数

        public static int g_iNgCount = 0;                  //NG计数     
        public static int g_iProdSeri = 0;                 //产品序号
        public static int g_iProdCT = 0;                 //CT

        public static int g_iProduct = 0;                  //定位拍照成功g_iProduct++  2#机器人取料成功g_iProduct--
        public static long g_lCT = 0;                       //CT
        public static long g_lRecord = 0;                   //记录CT的起始时间
        public static int g_iCountUph = 0;                 //每分钟产能

        public static bool g_bNgChange = false;                 //B品回收模式

        #endregion

        #region Lock

        static object LockWrite = new object();

        static object LockInsertsg = new object();
        static object LockCam0 = new object();
        static object LockCam1 = new object();

        static object LockWriteSeri = new object();

        static object LockWriteResult = new object();

        #endregion

        #region Initial

        public static FormMain Instance;

        public FormMain()
        {
            InitializeComponent();

            Instance = this;

            FrmWndRender = new WndRender();

            InsertRunMsg += new dInsertRunMsg(UpdateDataRunMsg);
            formMainDelegate += new FormMainDelegate(ProcessFormMaindDelegate);
            FormProductParameter.productDlgSendToMainDlg += new FormProductParameter.ProductDlgSendToMainDlg(ProcessProductDlgMsg);
            FormRobot.robotDlgSendToMainDlg += new FormRobot.RobotDlgSendToMainDlg(ProcessRobotDlgMsg);


        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            InsertRunMsg("正在初始化系统...", false);
            UpdataProductList();
            InitCSV();

            #region VisionInitial
            #endregion

            FrmWndRender.RestoreControls(this);
            this.WindowState = FormWindowState.Maximized;
            initVariable();  //初始化全局变量

            timer_CountUph.Start();

            button_auto_run.Enabled = true;
            ThStart();


        }

        #endregion

        #region Close

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (g_iMachineState == (int)MachineState.Run)
            {
                MessageBox.Show("设备正在运行中！", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            if (DialogResult.OK == MessageBox.Show("你确定要退出吗？", "关闭提示",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question))
            {

                g_iMachineState = (int)MachineState.Stop;

                strProductName = comboBox_select_product.SelectedItem.ToString();
                try
                {
                    string IniPath = "./CommunicationPar.ini";
                    if (!File.Exists("./CommunicationPar.ini"))
                    {
                        FileStream fs = File.Create("./CommunicationPar.ini");
                        fs.Close();
                    }
                    InIClass IniCommunication = new InIClass(IniPath);
                    IniCommunication.Write("ProductName", "Exit", strProductName);

                    if (comboBox_select_product.SelectedItem.ToString() != string.Empty)
                    {
                        if (!Directory.Exists("product"))
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo("product");
                            directoryInfo.Create();
                        }
                        if (!Directory.Exists("product/" + strProductName))
                        {
                            DirectoryInfo directoryInfo1 = new DirectoryInfo("product/" + strProductName);
                            directoryInfo1.Create();
                        }


                        //其它参数
                        if (!File.Exists("product/" + strProductName + "/other.ini"))
                        {
                            FileStream fs = File.Create("product/" + strProductName + "/other.ini");
                            fs.Close();
                        }

                        IniPath = "product/" + strProductName + "/other.ini";

                        InIClass IniOther = new InIClass(IniPath);

                        IniOther.Write("other", "textBox_A", textBox_A.Text);
                        IniOther.Write("other", "textBox_B", textBox_B.Text);
                        IniOther.Write("other", "textBox_C", textBox_C.Text);
                        IniOther.Write("other", "textBox_total", textBox_total.Text);

                        IniOther.Write("other", "g_iNgCount", g_iNgCount.ToString());
                        IniOther.Write("other", "g_iOkCount", g_iOkCount.ToString());

                        IniOther.Write("other", "lab_ClearInfoTime", lab_ClearInfoTime.Text);

                    }
                }
                catch (HalconException CvEx)
                {
                    MessageBox.Show(CvEx.Message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }




                if (g_bCam0Open)
                {
                    // CamDevice[0].SetParam((float)decimal.ToDouble(FormProdctPar.UpDown_Cam0_ExposureTime.Value));
                    // CamDevice[0].CloseGrab();
                }
                if (g_bCam1Open)
                {
                    //CamDevice[1].SetParam((float)decimal.ToDouble(FormProdctPar.UpDown_Cam1_ExposureTime.Value));
                    // CamDevice[1].CloseGrab();
                }
                if (ThInitDevice.IsAlive) { ThInitDevice.Abort(); }
                if (thContinueGrabCam0.IsAlive) { thContinueGrabCam0.Abort(); }
                if (thContinueGrabCam1.IsAlive) { thContinueGrabCam1.Abort(); }
                if (thLoadCap.IsAlive) { thLoadCap.Abort(); }
                if (thAOI.IsAlive) { thAOI.Abort(); }



                this.FormClosing -= new FormClosingEventHandler(this.FormMain_FormClosing); //这里是  -=
                Application.Exit();  //退出进程
            }
            else
            {
                e.Cancel = true;  //取消。返回窗体
            }
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            System.Environment.Exit(0);
        }

        #endregion

        #region UI

        #region Menu

        private void 登入系统ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*
            if (!g_bConnectIcReader)
            {
                MessageBox.Show("请检查IC卡机是否连接正常！", "读卡设备未连接");
                return;
            }

            if (!File.Exists("IcInfo.tup"))
            {
                MessageBox.Show("IcInfo.tup文件缺失!", "无法登录");
                return;
            }

            FormLog.Visible = true;
            //FormLog.ShowDialog();
            */
            FormLog.Visible = true;
        }

        private void 权限管理ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*
            if (!g_bConnectIcReader)
            {
                MessageBox.Show("请检查IC卡机是否连接正常！", "连接错误");
                return;
            }

            if (!File.Exists("IcInfo.tup"))
            {
                MessageBox.Show("IcInfo.tup文件已经缺失,请恢复该文件！", "文件错误");
                return;
            }

            FormLimit.Show();

            mainDlgSendToLimitDlg("LoadTupleFile", "");
            */
        }

        private void 注销登录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button_auto_run.Enabled = false;
            权限管理ToolStripMenuItem.Enabled = false;
            注销登录ToolStripMenuItem.Enabled = false;
            产品型号工艺参数配置ToolStripMenuItem.Enabled = false;
            通信与标定设置ToolStripMenuItem.Enabled = false;
            通信连接设置IC读卡器光源控制器MES系统各IO开关ToolStripMenuItem.Enabled = false;

            登入系统ToolStripMenuItem.Enabled = true;

            if (g_iLimit == (int)Limit.Operator)
            {
                InsertRunMsg("操作员退出！", false);
            }

            if (g_iLimit == (int)Limit.Engineer)
            {
                InsertRunMsg("工程师退出！", false);
            }

            g_strOperaterName = "test";
        }

        private void 产品型号工艺参数配置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //FrmLogin frmLogin = new FrmLogin();
            //if (frmLogin.ShowDialog() != DialogResult.OK)
            //{
            //    return;
            //}          
            FormProdctPar.Show();

        }

        private void 通信与标定设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormRbt.Show();
            //FormRbt.TopMost = true;
        }

        private void 关于ToolStripMenuItem1_Click(object sender, EventArgs e)
        {

        }

        private void ToolStripDATE_Click(object sender, EventArgs e)
        {
            string path = @"..\data";
            System.Diagnostics.Process.Start("explorer.exe", path);
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            try
            {
                AboutBox1 frmAbout = new AboutBox1();
                frmAbout.ShowDialog();
                if (frmAbout.DialogResult == DialogResult.Cancel)
                {
                    frmAbout.Dispose();
                    frmAbout.Close();
                }

            }
            catch (Exception)
            {

            }
        }

        #endregion

        #region Camera Button

        private void FormMain_Resize(object sender, EventArgs e)
        {
            try
            {
                FrmWndRender.ResizeControls(this);
            }
            catch (Exception ex)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
            }
        }

        int itest = 0;
        private void btn_SaveChart_Click(object sender, EventArgs e)
        {
            DateTime Time = DateTime.Now;
            chart.SaveImage(@"..\data\" + DateTime.Today.ToString("yyyy-MM-dd"), ChartImageFormat.Bmp);
            MessageBox.Show("数据分布图存至 Data 文件夹，单击菜单栏的 【报表查看】 即可查看");
            //itest++;
            //dataListg_iProdSeri.Add(itest);
            //Random AAA = new Random();

            //double ARandomdata = AAA.Next(4808, 4816);
            //ARandomdata = ARandomdata / 100;
            //dataListAA.Add(ARandomdata);

            //ARandomdata = AAA.Next(4808, 4816);
            //ARandomdata = ARandomdata / 100;
            //dataListBB.Add(ARandomdata);

            //ARandomdata = AAA.Next(4808, 4816);
            //ARandomdata = ARandomdata / 100;
            //dataListCC.Add(ARandomdata);

            //ARandomdata = AAA.Next(4808, 4816);
            //ARandomdata = ARandomdata / 100;
            //dataListDD.Add(ARandomdata);



            //RefreshChart(dataListg_iProdSeri, dataListAA, dataListBB, dataListCC, dataListDD);

        }


        private void btn_ClearInfo_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == MessageBox.Show("你确定要清除产量信息吗？", "温馨提示",
           MessageBoxButtons.OKCancel, MessageBoxIcon.Question))
            {
                textBox_A.Text = "0";
                textBox_B.Text = "0";
                textBox_C.Text = "0";
                textBox_total.Text = "0";
                textBox_A_rate.Text = "0";
                textBox_ng_count.Text = "0";

                g_iNgCount = 0;
                g_iOkCount = 0;
                g_iisNGBCount = 0;
                g_iisNGBCount = 0;
                g_iisNGCCount = 0;
                g_iTotal = 0;

                DateTime dt = DateTime.Now;
                lab_ClearInfoTime.Text = "LastClearTime：" + DateTime.Today.ToString("yyyy-MM-dd") + "  " + dt.ToLongTimeString();


                dataGridView_Result.Rows.Clear();
            }

        }

        //cam0 UI

        private void button_cam0_connect_Click(object sender, EventArgs e)
        {
            try
            {
                if (button_cam0_connect.Text == "断开连接")
                {
                    CamDevice[0].CloseGrab();
                    button_cam0_connect.Text = "连接相机";
                    InsertRunMsg("0#相机已经断开连接！！！", false);
                    g_bCam0Open = false;
                    button_cam0_continue_grab.Enabled = false;
                    button_cam0_grab_image.Enabled = false;
                    pictureBox_cam0_status_light.BackColor = Color.Red;
                }
                else
                {
                    if (CamDevice[0].InitDeviceAcq(g_strCamSerial, 0))
                    {
                        button_cam0_connect.Text = "断开连接";
                        InsertRunMsg("0#相机已连接.", false);
                        g_bCam0Open = true;
                        button_cam0_continue_grab.Enabled = true;
                        button_cam0_grab_image.Enabled = true;
                        pictureBox_cam0_status_light.BackColor = Color.Lime;
                    }
                    else
                    {
                        MessageBox.Show("连接失败！", "提示",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam0_continue_grab_Click(object sender, EventArgs e)
        {
            try
            {
                if (!g_bCam0Open)
                {
                    MessageBox.Show("0#相机未连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                g_bCam0ContinueGrab = !g_bCam0ContinueGrab;
                if (g_bCam0ContinueGrab)
                {
                    button_cam0_continue_grab.Text = "停止采集";
                    button_cam0_grab_image.Enabled = false;
                    button_cam0_connect.Enabled = false;
                    button_cam0_load_image.Enabled = false;
                    button_cam0_test_image.Enabled = false;
                    button_cam0_save_result_image.Enabled = false;
                }
                else
                {
                    button_cam0_continue_grab.Text = "连续采集";
                    button_cam0_grab_image.Enabled = true;
                    button_cam0_connect.Enabled = true;
                    button_cam0_load_image.Enabled = true;
                    button_cam0_test_image.Enabled = true;
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam0_grab_image_Click(object sender, EventArgs e)
        {
            try
            {
                if (!g_bCam0Open)
                {
                    MessageBox.Show("0#相机未连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                GrabImage(Cam0, SoftTrigger);
                hWin0.DisPlay(ho_SrcImg[0].Clone(), null, string.Empty);

                InsertRunMsg("0#相机已成功获取图片.", false);
                button_cam0_grab_image.Enabled = true;
                button_cam0_test_image.Enabled = true;
                button_cam0_save_original_image.Enabled = true;
                button_cam0_fit.Enabled = true;
                button_cam0_load_image.Enabled = true;
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam0_load_image_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Title = "打开图片";
            ofd.Filter = "图片|*.bmp;*.jpg";

            try
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    HOperatorSet.ReadImage(out ho_SrcImg[0], ofd.FileName);

                    HTuple chanels = new HTuple();
                    HOperatorSet.CountChannels(ho_SrcImg[0], out chanels);
                    if (chanels == 3)
                    {
                        HObject r, g, b;
                        HOperatorSet.Decompose3(ho_SrcImg[0], out r, out g, out b);
                        HOperatorSet.Rgb3ToGray(r, g, b, out ho_SrcImg[0]);
                    }

                    hWin0.DisPlay(ho_SrcImg[0].Clone(), null, string.Empty, true, false, true);
                    button_cam0_test_image.Enabled = true;
                    button_cam0_save_original_image.Enabled = true;
                    button_cam0_fit.Enabled = true;
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam0_test_image_Click(object sender, EventArgs e)
        {

            FormProdctPar.checkBox_show_result_image_cam0.Checked = true;
            Cam0Detect(ho_SrcImg[3].Clone(), Convert.ToInt32(textBox1.Text), out HTuple hv_CodeDateString, out HObject outLoadResultImage, out double Xoff, out double Yoff, out double Uoff);
            FormProdctPar.checkBox_show_result_image_cam0.Checked = false;

        }

        private void button_cam0_save_original_image_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "保存图片";
            sfd.Filter = "图片|*.bmp;*.jpg";

            try
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    if (ho_SrcImg[3].IsInitialized())
                    {
                        HOperatorSet.WriteImage(ho_SrcImg[3], "bmp", 0, sfd.FileName);
                        InsertRunMsg("图片已保存到" + sfd.FileName, false);
                        MessageBox.Show("图片保存成功！", "提示");
                    }
                    else
                    {
                        InsertRunMsg("没有数据,保存失败", false);
                        MessageBox.Show("没有数据!!!", "保存失败");
                    }
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam0_save_result_image_Click(object sender, EventArgs e)
        {
            if (FormMain.ho_RstImg[0].IsInitialized())
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "保存图片";
                sfd.Filter = "图片|*.bmp;*.jpg";

                try
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        if (FormMain.ho_RstImg[0].IsInitialized())
                        {
                            HOperatorSet.WriteImage(FormMain.ho_RstImg[0], "bmp", 0, sfd.FileName);
                            MessageBox.Show("图片保存成功！", "提示");
                        }
                        else
                        {
                            MessageBox.Show("没有数据!!!", "保存失败");
                        }
                    }
                }
                catch (HalconException CvEx)
                {
                    MessageBox.Show(CvEx.Message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void button_cam0_fit_Click(object sender, EventArgs e)
        {
            if (ho_SrcImg[0].IsInitialized())
            {
                hWin0.DisPlay(ho_SrcImg[0].Clone(), null, "", true, true, true);
            }
        }

        //cam1 UI
        private void button_cam1_connect_Click(object sender, EventArgs e)
        {
            try
            {
                if (button_cam1_connect.Text == "断开连接")
                {
                    CamDevice[1].CloseGrab();
                    button_cam1_connect.Text = "连接相机";
                    InsertRunMsg("1#相机已经断开连接！！！", false);
                    g_bCam1Open = false;
                    button_cam1_continue_grab.Enabled = false;
                    pictureBox_cam1_status_light.BackColor = Color.Red;
                }
                else
                {
                    if (CamDevice[1].InitDeviceAcq(g_strCamSerial, 1))
                    {
                        button_cam1_connect.Text = "断开连接";
                        InsertRunMsg("1#相机已连接.", false);
                        g_bCam1Open = true;
                        button_cam1_continue_grab.Enabled = true;
                        pictureBox_cam1_status_light.BackColor = Color.Lime;
                    }
                    else
                    {
                        MessageBox.Show("连接失败！", "提示",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam1_continue_grab_Click(object sender, EventArgs e)
        {
            try
            {
                if (!g_bCam1Open)
                {
                    MessageBox.Show("1#相机未连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                g_bCam1ContinueGrab = !g_bCam1ContinueGrab;
                if (g_bCam1ContinueGrab)
                {
                    button_cam1_continue_grab.Text = "停止采集";
                    button_cam1_grab_image.Enabled = true;
                    button_cam1_connect.Enabled = false;
                    button_cam1_load_image.Enabled = false;
                    button_cam1_test_image.Enabled = false;
                    button_cam1_save_original_image.Enabled = true;
                    button_cam1_save_result_image.Enabled = false;
                    button_cam1_fit.Enabled = true;

                }
                else
                {
                    button_cam1_continue_grab.Text = "连续采集";
                    button_cam1_grab_image.Enabled = false;
                    button_cam1_connect.Enabled = true;
                    button_cam1_load_image.Enabled = true;
                    button_cam1_test_image.Enabled = false;
                    button_cam1_save_original_image.Enabled = false;
                    button_cam1_fit.Enabled = false;

                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam1_grab_image_Click(object sender, EventArgs e)
        {
            try
            {
                if (!g_bCam1Open)
                {
                    MessageBox.Show("1#相机未连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                GrabImage(Cam0, SoftTrigger);
                InsertRunMsg("1#相机已成功获取图片.", false);
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }

        private void button_cam1_load_image_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Title = "打开图片";
            ofd.Filter = "图片|*.bmp;*.jpg";

            try
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    HOperatorSet.ReadImage(out ho_SrcImg[1], ofd.FileName);
                    // HOperatorSet.MedianRect(SrcImg, out SrcImg, 3.5, 3.5);

                    HTuple chanels = new HTuple();
                    HOperatorSet.CountChannels(ho_SrcImg[1], out chanels);
                    if (chanels == 3)
                    {
                        HObject r, g, b;
                        HOperatorSet.Decompose3(ho_SrcImg[1], out r, out g, out b);
                        HOperatorSet.Rgb3ToGray(r, g, b, out ho_SrcImg[1]);
                    }

                    hWin1.DisPlay(ho_SrcImg[1].Clone(), null, string.Empty);
                    button_cam1_test_image.Enabled = true;
                    button_cam1_save_original_image.Enabled = true;
                    button_cam1_fit.Enabled = true;
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam1_test_image_Click(object sender, EventArgs e)
        {
            StartTimeAOI = DateTime.Now;
            FormProdctPar.checkBox_show_result_image_cam1.Checked = true;

            Cam1Detect(ho_SrcImg[1].Clone(), 1, Convert.ToInt32(FormProdctPar.cmb_MultiLine.Text), Convert.ToDouble(FormProdctPar.numericUpDown_MultiAngle.Value),
                Convert.ToDouble(FormProdctPar.NDU_AAoff.Value), Convert.ToDouble(FormProdctPar.NDU_BBoff.Value),
                Convert.ToDouble(FormProdctPar.NDU_CCoff.Value), Convert.ToDouble(FormProdctPar.NDU_DDoff.Value),
                out HTuple CodeDateString, out HObject resultImage, out HTuple Lengthes);

            FormProdctPar.checkBox_show_result_image_cam1.Checked = false;

            SpanTimeAOI = DateTime.Now - StartTimeAOI;
            ElspseTimeAOI = SpanTimeAOI.TotalMilliseconds;
            lab_AOITime1.Text = ElspseTimeAOI.ToString() + "ms";

            hWin1.DisPlay(resultImage.Clone(), null, string.Empty);
        }


        private void button_cam1_save_original_image_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "保存图片";
            sfd.Filter = "图片|*.bmp;*.jpg";

            try
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    if (ho_SrcImg[1].IsInitialized())
                    {
                        HOperatorSet.WriteImage(ho_SrcImg[1], "bmp", 0, sfd.FileName);
                        InsertRunMsg("图片已保存到" + sfd.FileName, false);
                        MessageBox.Show("图片保存成功！", "提示");
                    }
                    else
                    {
                        InsertRunMsg("没有数据,保存失败", false);
                        MessageBox.Show("没有数据!!!", "保存失败");
                    }
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_cam1_save_result_image_Click(object sender, EventArgs e)
        {
            if (FormMain.ho_RstImg[1].IsInitialized())
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "保存图片";
                sfd.Filter = "图片|*.bmp;*.jpg";

                try
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        if (FormMain.ho_RstImg[1].IsInitialized())
                        {
                            HOperatorSet.WriteImage(FormMain.ho_RstImg[1], "bmp", 0, sfd.FileName);
                            MessageBox.Show("图片保存成功！", "提示");
                        }
                        else
                        {
                            MessageBox.Show("没有数据!!!", "保存失败");
                        }
                    }
                }
                catch (HalconException CvEx)
                {
                    MessageBox.Show(CvEx.Message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void button_cam1_fit_Click(object sender, EventArgs e)
        {
            if (ho_SrcImg[1].IsInitialized())
            {
                hWin1.DisPlay(ho_SrcImg[1].Clone(), null, "", true, true, true);
            }
        }

        #endregion

        #region Chart

        int UpmPoint = 0;
        //Upm 添加时间数据和对应列的值  
        public void AddUphPoint()
        {
            DateTime timeStamp = DateTime.Now;

            Series ptSeries = chart_Uph.Series[0];

            ptSeries.Points.AddXY(timeStamp.ToOADate(), g_iCountUph);

            UpmPoint++;

            chart_Uph.ChartAreas[0].AxisX.Minimum = ptSeries.Points[0].XValue;
            chart_Uph.ChartAreas[0].AxisX.Maximum = DateTime.FromOADate(ptSeries.Points[0].XValue).AddMinutes(UpmPoint + 15).ToOADate();

            chart_Uph.ChartAreas[0].AxisX.ScaleView.SizeType = DateTimeIntervalType.Minutes;
            chart_Uph.ChartAreas[0].AxisX.Interval = 1;
            chart_Uph.ChartAreas[0].AxisX.ScaleView.Size = 15;

            chart_Uph.ChartAreas[0].AxisX.ScaleView.Position = DateTime.FromOADate(ptSeries.Points[0].XValue).AddMinutes(UpmPoint - 15).ToOADate();

            chart_Uph.Invalidate();
        }
        #endregion

        #region Button
        //启停按钮
        private void button_auto_run_Click(object sender, EventArgs e)
        {
            SetRun();
        }

        private void button_stop_Click(object sender, EventArgs e)
        {

            if (g_iMachineState == (int)MachineState.Run)
            {
                if (DialogResult.OK == MessageBox.Show("你确定要停止运行吗？", "设备停止提示",
               MessageBoxButtons.OKCancel, MessageBoxIcon.Question))
                {
                    SetMachineState((int)MachineState.Stop);
                    // RecordImageIndex();

                }
            }

        }
        #endregion

        #endregion

        #region MyFunction
        //private void GrabImageMethod(HWin hWinID, int CamIndex, float ExposureValue, int ImageIndex, int Rotate)
        //{
        //    try
        //    {
        //        if (CamDevice[CamIndex].TrigImage != null) CamDevice[CamIndex].TrigImage.Dispose();
        //        CamDevice[CamIndex].TrigImage = new HObject();   //图像复位

        //        CamDevice[CamIndex].SetParam(ExposureValue);

        //        CamDevice[CamIndex].SetTriggerMode(7); //软触发
        //        CamDevice[CamIndex].SoftTriggerExec();
        //        ho_SrcImg[ImageIndex] = CamDevice[CamIndex].TrigImage.Clone();

        //        HOperatorSet.RotateImage(ho_SrcImg[ImageIndex], out ho_SrcImg[ImageIndex], Rotate, "bilinear");
        //        hWinID.DisPlay(ho_SrcImg[ImageIndex].Clone(), null, string.Empty);
        //    }
        //    catch (HalconException CvEx)
        //    {
        //        MessageBox.Show(CvEx.Message);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //    }

        //}

        private void initVariable()
        {
            string IniPath = "./CommunicationPar.ini";
            if (!File.Exists("./CommunicationPar.ini"))
            {
                FileStream fs = File.Create("./CommunicationPar.ini");
                fs.Close();
            }
            InIClass IniCommunication = new InIClass(IniPath);
            strProductName = IniCommunication.Read("ProductName", "Exit");

            comboBox_select_product.Text = strProductName;

            //其它参数
            if (!File.Exists("product/" + strProductName + "/other.ini"))
            {
                FileStream fs = File.Create("product/" + strProductName + "/other.ini");
                fs.Close();
            }

            IniPath = "product/" + strProductName + "/other.ini";

            InIClass IniOther = new InIClass(IniPath);

            textBox_A.Text = IniOther.Read("other", "textBox_A");
            textBox_B.Text = IniOther.Read("other", "textBox_B");
            textBox_C.Text = IniOther.Read("other", "textBox_C");


            g_iTotal = Convert.ToInt32(IniOther.Read("other", "g_iTotal"));
            g_iOkCount = Convert.ToInt32(IniOther.Read("other", "g_iOkCount"));
            g_iNgCount = Convert.ToInt32(IniOther.Read("other", "g_iNgCount"));

            lab_ClearInfoTime.Text = IniOther.Read("other", "lab_ClearInfoTime");


            //Load
            if (!File.Exists("product/" + strProductName + "/cam0par.ini"))
            {
                MessageBox.Show("product/" + strProductName + "/cam0par.ini", "文件缺失");
                return;
            }

            IniPath = "product/" + strProductName + "/cam0par.ini";

            InIClass Inicam0par = new InIClass(IniPath);

            int num = Convert.ToInt32(Inicam0par.Read("product", "roi_num"));
            Roi[] LoadRoi = new Roi[num];
            for (int i = 1; i <= num; i++)
            {
                string iniSection = "Load-" + i.ToString();
                LoadRoi[i - 1].Row1 = Convert.ToInt32(Inicam0par.Read(iniSection, "y1"));
                LoadRoi[i - 1].Column1 = Convert.ToInt32(Inicam0par.Read(iniSection, "x1"));
                LoadRoi[i - 1].Row2 = Convert.ToInt32(Inicam0par.Read(iniSection, "y2"));
                LoadRoi[i - 1].Column2 = Convert.ToInt32(Inicam0par.Read(iniSection, "x2"));
            }
            //读取
            HTuple hv_ProductName = strProductName;
            // ReadCapParams(out HObject ho_ModelImage, hv_ProductName, out HTuple hv_CalibDataTest, out HTuple hv_CodeHandle);
            genRectRoi(LoadRoi[0].Row1, LoadRoi[0].Column1, LoadRoi[0].Row2, LoadRoi[0].Column2, out ho_LoadRoiCode);
            genRectRoi(LoadRoi[1].Row1, LoadRoi[1].Column1, LoadRoi[1].Row2, LoadRoi[1].Column2, out ho_LoadRoiMark);


            //AOI
            strProductName = comboBox_select_product.SelectedItem.ToString();
            if (!File.Exists("product/" + strProductName + "/cam1par.ini"))
            {
                MessageBox.Show("product/" + strProductName + "/cam1par.ini", "文件缺失");
                return;
            }
            IniPath = "product/" + strProductName + "/cam1par.ini";
            InIClass Inicam1par = new InIClass(IniPath);

            num = Convert.ToInt32(Inicam1par.Read("product", "roi_num"));
            Roi[] AOIRoi = new Roi[num];
            for (int i = 1; i <= num; i++)
            {
                string iniSection = "Mark" + i.ToString();
                AOIRoi[i - 1].Row1 = Convert.ToInt32(Inicam1par.Read(iniSection, "y1"));
                AOIRoi[i - 1].Column1 = Convert.ToInt32(Inicam1par.Read(iniSection, "x1"));
                AOIRoi[i - 1].Row2 = Convert.ToInt32(Inicam1par.Read(iniSection, "y2"));
                AOIRoi[i - 1].Column2 = Convert.ToInt32(Inicam1par.Read(iniSection, "x2"));
            }
            //读取
            hv_ProductName = strProductName;
            genRectRoi(AOIRoi[0].Row1, AOIRoi[0].Column1, AOIRoi[0].Row2, AOIRoi[0].Column2, out ho_AOIRoiCode);
            genRectRoi(AOIRoi[1].Row1, AOIRoi[1].Column1, AOIRoi[1].Row2, AOIRoi[1].Column2, out ho_AOIRoiMark);



            //标准二维码ROI   MarkROI
            HObject resultImage = new HObject();
            modelModeCodeRow = Convert.ToDouble(Inicam1par.Read("ModleCode", "ModeCodeRow"));
            ModeCodeCol = Convert.ToDouble(Inicam1par.Read("ModleCode", "ModeCodeCol"));
            ModeCodeAngle = Convert.ToDouble(Inicam1par.Read("ModleCode", "ModeCodeAngle"));
            hv_CoordAngleOffset = Convert.ToDouble(FormProdctPar.UpDown_Cam1_CoordAngleOffset.Value);
            hv_CalibData1 = new HTuple();

            //*cam1 工艺参数
            stInspPara.DiaStd = Convert.ToDouble(FormProdctPar.UpDown_DiaStd.Value);
            stInspPara.DiaMaxValue = Convert.ToDouble(FormProdctPar.UpDown_upperDeviation.Value);
            stInspPara.DiaMinValue = Convert.ToDouble(FormProdctPar.UpDown_lowerDeviation.Value);

            for (int i = 0; i < 2; i++)
            {
                CamDevice[i] = new MVSCam();
            }

            for (int i = 0; i < 4; i++)
            {
                hWin[i] = new HalconDotNet.HWindow();
                ho_SrcImg[i] = new HObject();
                ho_RstImg[i] = new HObject();
            }
            g_iMachineState = (int)MachineState.Stop;

            //thAutoRun = new Thread(ThSatrt);

            thLoadCap = new Thread(LoadCap);
            thAOI = new Thread(AOI);

            thScanMCD = new Thread(ScanMCD);
            //thSaveImage = new Thread(SaveImage);

            thCountProcessResult = new Thread(ShowandWriteResult);
            thContinueGrabCam0 = new Thread(ContinueGrabCam0);
            thContinueGrabCam1 = new Thread(ContinueGrabCam1);

            Cam0ImageList = new SortedList<int, HObject>();
            Cam1ImageList = new SortedList<int, HObject>();
            Cam1Imagequeue = new Queue<HObject>();
            Cam0Imagequeue = new Queue<HObject>();


            HOperatorSet.CreateDataCode2dModel("QR Code", new HTuple(), new HTuple(), out QRCodeHandle);
            HOperatorSet.CreateDataCode2dModel("Data Matrix ECC 200", new HTuple(), new HTuple(), out ECC200CodeHandle);

            if (FormProdctPar.combCodeType.Text == "QR")
            {
                HOperatorSet.CreateDataCode2dModel("QR Code", new HTuple(), new HTuple(), out DataCodeHandle);
            }
            else
            {
                HOperatorSet.CreateDataCode2dModel("Data Matrix ECC 200", new HTuple(), new HTuple(), out DataCodeHandle);
            }

            ThInitDevice = new Thread(InitDevice);
            InsertRunMsgCnt = 0;
            AOIInspectIndex = 1;
            g_strOperaterName = FormProdctPar.textBox_Operate.Text;
            Cam0ImageIndex = 4;
            Cam1ImageIndex = 4;

        }

        private void InitCSV()
        {
            try
            {
                CSVSavePath = "../data/" + DateTime.Now.ToString("yyyyMMdd") + ".csv";
                if (!FileHelper.IsExistFile(CSVSavePath))
                {
                    FileHelper.AppendText(CSVSavePath,
                        string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                        "日期", "时间", "部门", "设备编号", "序号", "二维码", "AA", "BB", "CC", "DD", "检测结果", "通道/工位", "操作员"));
                }
            }

            catch (Exception ex)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);

            }


        }

        private void WriteCSV(int num)
        {
            HTuple hv_MSecond = null, hv_Second = null;
            HTuple hv_Minute = null, hv_Hour = null, hv_Day = null;
            HTuple hv_YDay = null, hv_Month = null, hv_Year = null;

            try
            {
                //InsertRunMsg("产品序号:" + num + "开始写.CSV", false);

                HOperatorSet.GetSystemTime(out hv_MSecond, out hv_Second, out hv_Minute, out hv_Hour, out hv_Day, out hv_YDay, out hv_Month, out hv_Year);
                hv_Year = hv_Year.TupleString(".4");
                hv_Month = hv_Month.TupleString(".2");
                hv_Day = hv_Day.TupleString(".2");
                hv_Hour = hv_Hour.TupleString(".2");
                hv_Minute = hv_Minute.TupleString(".2");
                hv_Second = hv_Second.TupleString(".2");
                hv_MSecond = hv_MSecond.TupleString(".3");
                //    "日期", "时间", "部门", "设备编号","序号" "二维码", "AA", "BB", "CC", "DD", "检测结果", "通道/工位","操作员";
                if (g_bNgChange==true)
                {
                    CSVSavePath= "D\\Data\\BNG";
                }
                FileHelper.AppendText(CSVSavePath, string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                         hv_Year.S + "/" + hv_Month.S + "/" + hv_Day.S,
                         hv_Hour.S + ":" + hv_Minute.S + ":" + hv_Second.S,
                         FormProdctPar.txt_Department.Text,
                         FormProdctPar.textBox_MachineID.Text,
                         num.ToString(),
                         inspectResult[g_iProdSeri].Qrcode,
                          Convert.ToDouble(inspectResult[g_iProdSeri].g_dAALength).ToString("#0.0000"),
                          Convert.ToDouble(inspectResult[g_iProdSeri].g_dBBLength).ToString("#0.0000"),
                          Convert.ToDouble(inspectResult[g_iProdSeri].g_dCCLength).ToString("#0.0000"),
                          Convert.ToDouble(inspectResult[g_iProdSeri].g_dDDLength).ToString("#0.0000"),
                         inspectResult[g_iProdSeri].strInspectResult,
                         inspectResult[g_iProdSeri].channel,
                         g_strOperaterName));

                InsertRunMsg("序号：" + num + "二维码：" + inspectResult[g_iProdSeri].Qrcode + "写EXCEL完成  ！", false);

            }
            catch (HalconException CvEx)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(CvEx.StackTrace, "06") + CvEx.Message);

            }
            catch (Exception ex)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);

            }

        }

        private void InitFace()
        {
            InIClass readLoginSet = new InIClass(strComParIniPath);

            comboBox_select_product.Text = readLoginSet.Read("ProductName", "Exit");
            comboBox_select_product.Enabled = false;
            button_auto_run.Enabled = true;

            if (readLoginSet.Read("IcReader", "UseIc") != "0") return;
            button_auto_run.Enabled = false;
            权限管理ToolStripMenuItem.Enabled = false;
            注销登录ToolStripMenuItem.Enabled = false;
            产品型号工艺参数配置ToolStripMenuItem.Enabled = false;
            通信与标定设置ToolStripMenuItem.Enabled = false;
            通信连接设置IC读卡器光源控制器MES系统各IO开关ToolStripMenuItem.Enabled = false;

        }

        private void initChart()
        {

            //Uph
            chart_Uph.Series[0].ChartType = SeriesChartType.Line;
            chart_Uph.Series[0].IsValueShownAsLabel = true;    //图上显示数据点的值
            chart_Uph.Series[0].ToolTip = "#VALX,#VALY";       //鼠标停留在数据点上，显示XY值

            //启用X游标，以支持局部区域选择放大
            chart_Uph.ChartAreas[0].CursorX.IsUserEnabled = true;
            chart_Uph.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            chart_Uph.ChartAreas[0].CursorX.LineColor = Color.Pink;
            chart_Uph.ChartAreas[0].CursorX.IntervalType = DateTimeIntervalType.Auto;
            chart_Uph.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
            chart_Uph.ChartAreas[0].AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.All;   //启用X轴滚动条按钮
            chart_Uph.ChartAreas[0].AxisX.ScrollBar.ButtonColor = Color.Teal;
            chart_Uph.ChartAreas[0].AxisX.ScrollBar.LineColor = Color.DarkSlateGray;

            minValue = DateTime.Now;            //x轴最小刻度
            maxValue = minValue.AddMinutes(30); //X轴最大刻度,比最小刻度大30秒
            chart_Uph.ChartAreas[0].AxisX.LabelStyle.Format = "HH:mm";         //毫秒格式： hh:mm:ss.fff ，后面几个f则保留几位毫秒小数，此时要注意轴的最大值和最小值不要差太大
            chart_Uph.ChartAreas[0].AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Minutes;
            chart_Uph.ChartAreas[0].AxisX.LabelStyle.Interval = 1;                //坐标值间隔1分钟
            chart_Uph.ChartAreas[0].AxisX.LabelStyle.IsEndLabelVisible = false;   //防止X轴坐标跳跃
            chart_Uph.ChartAreas[0].AxisX.MajorGrid.IntervalType = DateTimeIntervalType.Minutes;
            chart_Uph.ChartAreas[0].AxisX.MajorGrid.Interval = 1;

            chart_Uph.ChartAreas[0].AxisX.Minimum = minValue.ToOADate();
            chart_Uph.ChartAreas[0].AxisX.Maximum = maxValue.ToOADate();
            chart_Uph.Series.Clear();

            Series newSeries = new Series("Series1");
            newSeries.ChartType = SeriesChartType.Line;
            newSeries.BorderWidth = 1;
            newSeries.Color = Color.Yellow;
            newSeries.XValueType = ChartValueType.DateTime;
            newSeries.MarkerColor = Color.Yellow;
            newSeries.MarkerSize = 10;
            newSeries.MarkerStyle = MarkerStyle.Square;
            newSeries.IsValueShownAsLabel = true;
            newSeries.LabelForeColor = Color.Yellow;
            chart_Uph.Series.Add(newSeries);



            AddUphPoint();
        }

        private void InitCam()
        {
            try
            {
                InsertRunMsg("正在连接相机...", false);

                string err = null;          // "00E46504565", "00E48812464"
                cam0 = new DoWithCamera("00E46504565", (float)FormProdctPar.UpDown_Cam0_ExposureTime.Value, UpdateShowInfo, out err);
                if (err != null)
                {
                    //Console.WriteLine("相机连接错误!");
                    InsertRunMsg("相机0连接错误***!", false);
                    return;
                }
                cam0.GetImageTodo = GetImageFromCam0Todo;
                cam0.UpdateInfo = UpdateShowInfo;
                InsertRunMsg("相机#0连接成功!", false);
                g_bCam0Open = true;
                if (g_bCam0Open)
                {
                    pictureBox_cam0_status_light.BackColor = Color.Lime;
                    button_cam0_connect.Text = "断开连接";
                    button_cam0_continue_grab.Enabled = true;
                    button_cam0_grab_image.Enabled = true;
                }

                err = null;
                cam1 = new DoWithCamera("00E48812464", (float)FormProdctPar.UpDown_Cam1_ExposureTime.Value, UpdateShowInfo, out err);
                if (err != null)
                {
                    //Console.WriteLine("相机连接错误!");
                    InsertRunMsg("相机1连接错误***!", false);
                    return;
                }
                cam1.GetImageTodo = GetImageFromCam1Todo;
                cam1.UpdateInfo = UpdateShowInfo;

                InsertRunMsg("相机#1连接成功!", false);
                g_bCam1Open = true;
                if (g_bCam1Open)
                {
                    pictureBox_cam1_status_light.BackColor = Color.Lime;
                    button_cam1_connect.Text = "断开连接";
                    button_cam1_continue_grab.Enabled = true;
                    button_cam1_grab_image.Enabled = true;
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void RecordImageIndex()
        {
            InIClass RecordImageIndex = new InIClass(strComParIniPath);


            RecordImageIndex.Write("Cam0", "ImageIndex", FormMain.Cam0ImageIndex.ToString());
            RecordImageIndex.Write("Cam1", "ImageIndex", FormMain.Cam1ImageIndex.ToString());
        }

        private void GrabImage(uint cameraIndex, uint TriggerMode)
        {
            try
            {
                uint CameraIndex = 0;

                switch (cameraIndex)
                {
                    case 0:
                        CameraIndex = hCam0;
                        break;

                    case 1:
                        CameraIndex = hCam1;
                        break;
                }
                if (CamDevice[CameraIndex].TrigImage != null) CamDevice[CameraIndex].TrigImage.Dispose();
                CamDevice[CameraIndex].TrigImage = new HObject();   //图像复位

                CamDevice[CameraIndex].SetTriggerMode(TriggerMode); //触发方式 0、硬触发   7、软触发          
                if (TriggerMode == 7)
                {
                    CamDevice[CameraIndex].SoftTriggerExec();
                }

                if (TriggerMode == 0)
                {
                    switch (CameraIndex)
                    {
                        case hCam0:
                            //   IOC1280_WriteIO(Card_0, (ushort)IOC1280_OUT.HardTriggerCam0, 0);
                            break;

                        case hCam1:
                            //  IOC1280_WriteIO(Card_0, (ushort)IOC1280_OUT.HardTriggerCam1, 0);
                            break;
                    }
                    CamDevice[CameraIndex].ManualTriggerExec();
                }

                ho_SrcImg[cameraIndex] = CamDevice[CameraIndex].TrigImage;
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public static void saveImage(string strCodeDate, HObject ho_Image, HTuple hv_CamIndex, HTuple hv_SaveType, int suckerIndex)
        {
            // Local iconic variables 

            // Local control variables 

            HTuple hv_MSecond = null, hv_Second = null;
            HTuple hv_Minute = null, hv_Hour = null, hv_Day = null;
            HTuple hv_YDay = null, hv_Month = null, hv_Year = null;
            HTuple hv_Date = null, hv_Time = null, hv_DateTime = null;
            HTuple hv_PathOfCCD_Images = null, hv_FileExists = null;
            HTuple hv_PathOfCCD_Images_Date = null, hv_SaveImagePath = new HTuple();
            HTuple hv_SaveImageName = null;


            try
            {
                // Initialize local and output iconic variables 
                HOperatorSet.GetSystemTime(out hv_MSecond, out hv_Second, out hv_Minute, out hv_Hour,
                out hv_Day, out hv_YDay, out hv_Month, out hv_Year);
                hv_Year = hv_Year.TupleString(".4");
                hv_Month = hv_Month.TupleString(".2");
                hv_Day = hv_Day.TupleString(".2");
                hv_Hour = hv_Hour.TupleString(".2");
                hv_Minute = hv_Minute.TupleString(".2");
                hv_Second = hv_Second.TupleString(".2");
                hv_MSecond = hv_MSecond.TupleString(".3");

                hv_Date = (hv_Year + hv_Month) + hv_Day;
                hv_Time = (hv_Hour + hv_Minute) + hv_Second;
                hv_DateTime = (hv_Date + " ") + hv_Time;
                hv_PathOfCCD_Images = "D:/SOFTWARE/CCD_Images/";
                HOperatorSet.FileExists(hv_PathOfCCD_Images, out hv_FileExists);
                if ((int)(new HTuple(hv_FileExists.TupleNotEqual(1))) != 0)
                {
                    HOperatorSet.MakeDir(hv_PathOfCCD_Images);
                }

                hv_PathOfCCD_Images_Date = hv_PathOfCCD_Images + hv_Date;
                HOperatorSet.FileExists(hv_PathOfCCD_Images_Date, out hv_FileExists);
                if ((int)(new HTuple(hv_FileExists.TupleNotEqual(1))) != 0)
                {
                    HOperatorSet.MakeDir(hv_PathOfCCD_Images_Date);
                }

                switch (hv_CamIndex.I)
                {
                    case 0:
                        hv_PathOfCCD_Images_Date = hv_PathOfCCD_Images_Date + "/cam0";
                        break;
                    case 1:
                        hv_PathOfCCD_Images_Date = hv_PathOfCCD_Images_Date + "/cam1";
                        break;
                }

                HOperatorSet.FileExists(hv_PathOfCCD_Images_Date, out hv_FileExists);
                if ((int)(new HTuple(hv_FileExists.TupleNotEqual(1))) != 0)
                {
                    HOperatorSet.MakeDir(hv_PathOfCCD_Images_Date);
                }

                switch (hv_SaveType.I)
                {
                    case 0:
                        hv_SaveImagePath = hv_PathOfCCD_Images_Date + "/SourceImage";
                        break;
                    case 1:
                        hv_SaveImagePath = hv_PathOfCCD_Images_Date + "/OkImage";
                        break;
                    case 2:
                        hv_SaveImagePath = hv_PathOfCCD_Images_Date + "/NgImage";
                        break;
                    default:
                        //输入了错误参数，直接保存在日期文件夹
                        hv_SaveImagePath = hv_PathOfCCD_Images_Date.Clone();
                        break;
                }

                HOperatorSet.FileExists(hv_SaveImagePath, out hv_FileExists);
                if ((int)(new HTuple(hv_FileExists.TupleNotEqual(1))) != 0)
                {
                    HOperatorSet.MakeDir(hv_SaveImagePath);
                }

                if (strCodeDate == null)
                {
                    strCodeDate = "";
                }
                hv_SaveImageName = (((hv_SaveImagePath + "/") + hv_DateTime) + "-") + hv_MSecond + "#" + strCodeDate + "#" + suckerIndex.ToString();

                if (hv_SaveType.I != 0)
                {
                    HOperatorSet.WriteImage(ho_Image, "jpeg 20", 0, hv_SaveImageName);
                    //HOperatorSet.WriteImage(ho_Image, "bmp", 0, hv_SaveImageName);
                }
                else
                {
                    //  HOperatorSet.WriteImage(ho_Image, "bmp", 0, hv_SaveImageName);
                    HOperatorSet.WriteImage(ho_Image, "jpeg 20", 0, hv_SaveImageName);

                }

                return;
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadCalibData()
        {
            HTuple hv_FileExist1, hv_FileExist2, hv_FileExist3, hv_FileExist4, hv_FileExist5, hv_FileExist6;

            try
            {
                HOperatorSet.FileExists("CalibData1.tup", out hv_FileExist1);
                HOperatorSet.FileExists("CalibData2.tup", out hv_FileExist2);
                HOperatorSet.FileExists("CalibData3.tup", out hv_FileExist3);
                HOperatorSet.FileExists("CalibData4.tup", out hv_FileExist4);
                HOperatorSet.FileExists("CalibData5.tup", out hv_FileExist5);
                HOperatorSet.FileExists("CalibData6.tup", out hv_FileExist6);

                if (hv_FileExist1 < 1)
                {
                    InsertRunMsg("加载 CalibFile_1 失败,请重新标定1#吸嘴9点", false);
                    return;
                }
                else
                {
                    HOperatorSet.ReadTuple("CalibData1.tup", out hv_CalibData1);
                }
                if (hv_FileExist2 < 1)
                {
                    InsertRunMsg("加载 CalibFile_2 失败,请重新标定2#吸嘴9点", false);
                    return;
                }
                else
                {
                    HOperatorSet.ReadTuple("CalibData2.tup", out hv_CalibData2);

                }
                if (hv_FileExist3 < 1)
                {
                    InsertRunMsg("加载 CalibFile_3 失败,请重新标定3#吸嘴9点", false);
                    return;
                }
                else
                {
                    HOperatorSet.ReadTuple("CalibData3.tup", out hv_CalibData3);
                }
                if (hv_FileExist4 < 1)
                {
                    InsertRunMsg("加载 CalibFile_4 失败,请重新标定4#吸嘴9点", false);
                    return;
                }
                else
                {
                    HOperatorSet.ReadTuple("CalibData4.tup", out hv_CalibData4);
                }
                if (hv_FileExist5 < 1)
                {
                    InsertRunMsg("加载 CalibFile_5 失败,请重新标定5#吸嘴9点", false);
                    return;
                }
                else
                {
                    HOperatorSet.ReadTuple("CalibData5.tup", out hv_CalibData5);
                }
                if (hv_FileExist6 < 1)
                {
                    InsertRunMsg("加载 CalibFile_6 失败,请重新标定6#吸嘴9点", false);
                    return;
                }
                else
                {
                    HOperatorSet.ReadTuple("CalibData6.tup", out hv_CalibData6);
                }
                InsertRunMsg("所有吸嘴标定文件已经加载完毕", false);
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void UpdateDataRunMsg(string ResultStr, bool IsClear = false)
        {
            lock (LockInsertsg)
            {
                InsertRunMsgCnt++;
                if (InsertRunMsgCnt > 9999)
                {
                    IsClear = true;
                    InsertRunMsgCnt = 0;
                }

                //DateTime now = DateTime.Now;
                //string time = now.Hour.ToString() + ":" + now.Minute.ToString() + ":" + now.Second.ToString() + ":" + now.Millisecond.ToString();

                try
                {
                    HTuple hv_MSecond = null, hv_Second = null;
                    HTuple hv_Minute = null, hv_Hour = null, hv_Day = null;
                    HTuple hv_YDay = null, hv_Month = null, hv_Year = null;
                    HTuple hv_Date = null, hv_Time = null, hv_DateTime = null;

                    HOperatorSet.GetSystemTime(out hv_MSecond, out hv_Second, out hv_Minute, out hv_Hour,
                    out hv_Day, out hv_YDay, out hv_Month, out hv_Year);
                    hv_Year = hv_Year.TupleString(".4");
                    hv_Month = hv_Month.TupleString(".2");
                    hv_Day = hv_Day.TupleString(".2");
                    hv_Hour = hv_Hour.TupleString(".2");
                    hv_Minute = hv_Minute.TupleString(".2");
                    hv_Second = hv_Second.TupleString(".2");
                    hv_MSecond = hv_MSecond.TupleString(".3");

                    hv_Date = (hv_Year + "/" + hv_Month + "/") + hv_Day + " ";
                    hv_Time = (hv_Hour + ":" + hv_Minute + ":") + hv_Second;
                    hv_DateTime =/* (hv_Date + " ") + */hv_Time + ":" + hv_MSecond;

                    string time = hv_DateTime;

                    if (IsClear) listBox_RunMsg.Items.Clear();

                    listBox_RunMsg.Items.Add(time + "  " + ResultStr);
                    int rowCount = listBox_RunMsg.Items.Count;
                    //  listBox_RunMsg.SelectedIndex = rowCount - 1;


                    //写日志
                    string errorLogFilePath = Path.Combine(strAppPath, "Log");
                    if (!Directory.Exists(errorLogFilePath))
                    {
                        Directory.CreateDirectory(errorLogFilePath);
                    }
                    string logFile = Path.Combine(errorLogFilePath, DateTime.Today.ToString("yyyy-MM-dd") + ".txt");
                    StreamWriter swLogFile = new StreamWriter(logFile, true, Encoding.Unicode);
                    swLogFile.WriteLine(time + "  " + ResultStr + "\r\n");
                    swLogFile.Close();
                    swLogFile.Dispose();
                }
                catch (HalconException)
                {
                    //throw CvEx;
                }
                catch (Exception ex)
                {
                    LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
                }
            }
        }

        private bool SetRun()
        {
            bool ret = false;
            try
            {
                if (!g_bCam0Open)
                {
                    MessageBox.Show("0#相机未连接!!!", "启动失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return ret;
                }
                if (!g_bCam1Open)
                {
                    MessageBox.Show("1#相机未连接!!!", "启动失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return ret;
                }
                if (PLC1 == null)
                {
                    MessageBox.Show("1#PLC未连接!!!", "启动失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return ret;
                }

                if (PLC3 == null)
                {
                    MessageBox.Show("3#PLC未连接!!!", "启动失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return ret;
                }

                SetMachineState((int)MachineState.Run);
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return ret;
        }

        private void ThStart()
        {
            thLoadCap.Start();
            thAOI.Start();
            thScanMCD.Start();
            // thSaveImage.Start();
            ThInitDevice.Start();
            thContinueGrabCam0.Start();
            thContinueGrabCam1.Start();
            thCountProcessResult.Start();
            button_auto_run.Enabled = false;
            g_isSL_OnOff = true;

        }

        private void SaveCam1Model()
        {
            try
            {
                hWin1.DisPlay(FormMain.ho_SrcImg[1], null, string.Empty);
                if (!hWin1.HObj.IsInitialized())
                {
                    MessageBox.Show("图像不存在");
                    return;
                }
                string strProductName = comboBox_select_product.SelectedItem.ToString();
                if (!File.Exists("product/" + strProductName + "/cam1par.ini"))
                {
                    MessageBox.Show("product/" + strProductName + "/cam1par.ini", "文件缺失");
                    return;
                }

                string IniPath = "product/" + strProductName + "/cam1par.ini";

                InIClass Inicam1par = new InIClass(IniPath);

                int num = Convert.ToInt32(Inicam1par.Read("product", "roi_num"));
                Roi[] MarkRoi = new Roi[num];
                for (int i = 1; i <= num; i++)
                {
                    string iniSection = "Mark" + i.ToString();
                    MarkRoi[i - 1].Row1 = Convert.ToInt32(Inicam1par.Read(iniSection, "y1"));
                    MarkRoi[i - 1].Column1 = Convert.ToInt32(Inicam1par.Read(iniSection, "x1"));
                    MarkRoi[i - 1].Row2 = Convert.ToInt32(Inicam1par.Read(iniSection, "y2"));
                    MarkRoi[i - 1].Column2 = Convert.ToInt32(Inicam1par.Read(iniSection, "x2"));
                }

                genRectRoi(MarkRoi[0].Row1, MarkRoi[0].Column1, MarkRoi[0].Row2, MarkRoi[0].Column2, out HObject ho_RoiCode);
                genRectRoi(MarkRoi[1].Row1, MarkRoi[1].Column1, MarkRoi[1].Row2, MarkRoi[1].Column2, out HObject ho_RoiMark);

                HTuple hv_CodeHandle;
                if (FormProdctPar.combCodeType.Text == "QR Code")
                {
                    HOperatorSet.CreateDataCode2dModel("QR Code", new HTuple(), new HTuple(), out hv_CodeHandle);
                }
                else
                {
                    HOperatorSet.CreateDataCode2dModel("Data Matrix ECC 200", new HTuple(), new HTuple(), out hv_CodeHandle);
                }

                FindCode(FormMain.ho_SrcImg[1], ho_RoiCode, out HObject ho_SymbolXLDs, hv_CodeHandle, out HTuple hv_DecodedDataStrings1, out HTuple hv_ModeCodeRow, out HTuple hv_ModeCodeColumn, out HTuple hv_ModeCodeAngle);
                if (hv_DecodedDataStrings1 != null)
                {
                    string ModeCodeRow = string.Join("", hv_ModeCodeRow);
                    string ModeCodeColumn = string.Join("", hv_ModeCodeColumn);
                    string ModeCodeAngle = string.Join("", hv_ModeCodeAngle);

                    Inicam1par.Write("ModleCode", "ModeCodeRow", ModeCodeRow);
                    Inicam1par.Write("ModleCode", "ModeCodeCol", ModeCodeColumn);
                    Inicam1par.Write("ModleCode", "ModeCodeAngle", ModeCodeAngle);

                    WriteCapModelParams(FormMain.ho_SrcImg[1], strProductName, 27, hv_CodeHandle);
                    hWin1.DisPlay(FormMain.ho_SrcImg[1], null, string.Empty);

                    mainDlgSendToProductDlg("DispCam1Rst", FormMain.ho_SrcImg[1]);
                }
                else
                {
                    MessageBox.Show("提取二维码失败");
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void UpdataProductList()
        {
            try
            {
                if (!Directory.Exists("product"))
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo("product");
                    directoryInfo.Create();
                }

                if (!File.Exists("productName.tup"))
                {
                    MessageBox.Show("文件    productName.tup    缺失！");
                    return;
                }

                comboBox_select_product.Items.Clear();

                string strProductName = string.Empty;
                HTuple hv_Length = 0, hv_ProductName = null;

                HOperatorSet.ReadTuple("productName.tup", out hv_ProductName);
                HOperatorSet.TupleLength(hv_ProductName, out hv_Length);
                for (int i = 0; i < hv_Length; i++)
                {
                    strProductName = hv_ProductName[i];
                    comboBox_select_product.Items.Add(strProductName);
                    comboBox_select_product.SelectedIndex = 0;
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void RefreshChart(List<int> Listg_iProdSeri, List<double> ListAA, List<double> ListBB, List<double> ListCC, List<double> ListDD)
        {
            Double UP = Convert.ToDouble(FormProdctPar.UpDown_DiaStd.Value) + Convert.ToDouble(FormProdctPar.UpDown_upperDeviation.Value);
            Double low = Convert.ToDouble(FormProdctPar.UpDown_DiaStd.Value) - Convert.ToDouble(FormProdctPar.UpDown_lowerDeviation.Value);

            dataUP.Add(UP);
            dataLOW.Add(low);

            chart.Series[0].Points.DataBindXY(Listg_iProdSeri, ListAA);
            chart.Series[4].Points.DataBindXY(Listg_iProdSeri, dataUP);
            chart.Series[5].Points.DataBindXY(Listg_iProdSeri, dataLOW);

            chart.Series[1].Points.DataBindXY(Listg_iProdSeri, ListBB);
            chart.Series[6].Points.DataBindXY(Listg_iProdSeri, dataUP);
            chart.Series[7].Points.DataBindXY(Listg_iProdSeri, dataLOW);

            chart.Series[2].Points.DataBindXY(Listg_iProdSeri, ListCC);
            chart.Series[8].Points.DataBindXY(Listg_iProdSeri, dataUP);
            chart.Series[9].Points.DataBindXY(Listg_iProdSeri, dataLOW);

            chart.Series[3].Points.DataBindXY(Listg_iProdSeri, ListDD);
            chart.Series[10].Points.DataBindXY(Listg_iProdSeri, dataUP);
            chart.Series[11].Points.DataBindXY(Listg_iProdSeri, dataLOW);

        }

        private void SetMachineState(int state)
        {
            switch (state)
            {
                case (int)MachineState.Run:

                    g_iMachineState = (int)MachineState.Run;
                    button_machine_state.Text = "Running...";
                    button_machine_state.BackColor = Color.Lime;
                    button_auto_run.Text = "...";
                    button_auto_run.Enabled = false;
                    button_stop.Enabled = true;
                    InsertRunMsg("Start Running...", false);
                    textBox1.Visible = false;
                    gbxCam0button.Visible = false;
                    gbxCam1Button.Visible = false;

                    break;
                case (int)MachineState.Stop:
                    g_iMachineState = (int)MachineState.Stop;
                    button_machine_state.Text = "STOP";
                    button_machine_state.BackColor = Color.Red;
                    button_auto_run.Text = "RUN";
                    textBox1.Visible = true;
                    button_stop.Enabled = false;
                    button_auto_run.Enabled = true;
                    InsertRunMsg("Stop Run", false);
                    gbxCam0button.Visible = true;
                    gbxCam1Button.Visible = true;

                    break;
            }
        }

        private static void InitInspectResult(int prodSeri)
        {
            inspectResult[prodSeri].Cam1ProcDone = 1;
            inspectResult[prodSeri].Img1ProcHaveObj = 0;
            inspectResult[prodSeri].Img1ProcIsOk = 0;

            inspectResult[prodSeri].bWriteRecord = false;
            inspectResult[prodSeri].strInspectResult = "A";

        }

        public bool GetMcPlc(String buff, Plc Plc, out List<String> str)
        {
            str = new List<String>();

            // “D 10”的模式
            PlcDeviceType type;
            int addr;
            McProtocolApp.GetDeviceCode(buff.ToUpper(), out type, out addr);

            int n;
            int rtCode;
            if (McProtocolApp.IsBitDevice(type))
            {
                var data = new int[1];
                rtCode = Plc.GetBitDevice
                    (buff, data.Length, data);
                n = data[0];
            }
            else
            {
                rtCode = Plc.GetDevice(buff.ToUpper(), out n);
            }
            str.Add(n.ToString(CultureInfo.InvariantCulture));
            //listBox1.Items.Add(buff.ToUpper() + "=" + n.ToString(CultureInfo.InvariantCulture));
            if (0 < rtCode)
            {
                str.Add("ERROR:0x" + rtCode.ToString("X4"));
                // listBox1.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
            }
            return true;

        }

        public bool SetMcPlc(String buff, Plc Plc)
        {
            if (0 < buff.IndexOf('='))
            {
                string[] s = buff.Split('=');

                // “D 10=0”的模式
                PlcDeviceType type;
                int addr;
                McProtocolApp.GetDeviceCode(s[0], out type, out addr);

                //int val = int.Parse(s[1]) | 0x0002 | 0x0004 | 0x0008 | 0x0010;
                int val = int.Parse(s[1]);
                int rtCode;
                if (McProtocolApp.IsBitDevice(type))
                {
                    var data = new int[1];
                    data[0] = val;
                    rtCode = Plc.SetBitDevice(s[0], data.Length, data);
                }
                else
                {
                    rtCode = Plc.SetDevice(s[0], val);
                }
                if (0 < rtCode)
                {
                    return false;
                }
                string strPLC = string.Empty;
                if (Plc == PLC1)
                {
                    strPLC = "PLC1";

                }
                else if ((Plc == PLC2))
                {
                    strPLC = "PLC2";

                }
                else
                {
                    strPLC = "PLC3";
                }
                InsertRunMsg("send date to" + strPLC + "  " + s[0] + " =" + val.ToString(), false);

                return true;

            }
            else
            {
                return false;
            }

        }

        private void DataGridView_Result_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                dataGridView_Result.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            catch (Exception ex)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
            }
        }

        private void ShowResultMsg(int totalNum, DateTime time, string QrCode,
            double AA,
            double BB,
            double CC,
            double DD,
            string strResult,
            int Chanel,
            bool IsClear = false)
        {
            try
            {
                if (IsClear) { dataGridView_Result.Rows.Clear(); }
                //  "时间", "部门", "设备编号", "二维码", "AA", "BB", "CC", "DD", "检测结果", "操作员"));
                //显示最近10000行数据
                int rowCount = 0;
                if (dataGridView_Result.Rows.Count < 10000)
                {

                    dataGridView_Result.Rows.Add();
                    rowCount = dataGridView_Result.Rows.Count;
                    dataGridView_Result.Rows[rowCount - 1].Cells[0].Value = totalNum;
                    dataGridView_Result.Rows[rowCount - 1].Cells[1].Value = time;
                    dataGridView_Result.Rows[rowCount - 1].Cells[2].Value = QrCode;
                    dataGridView_Result.Rows[rowCount - 1].Cells[3].Value = double.Parse(AA.ToString("0.000"));
                    dataGridView_Result.Rows[rowCount - 1].Cells[4].Value = double.Parse(BB.ToString("0.000"));
                    dataGridView_Result.Rows[rowCount - 1].Cells[5].Value = double.Parse(CC.ToString("0.000"));
                    dataGridView_Result.Rows[rowCount - 1].Cells[6].Value = double.Parse(DD.ToString("0.000"));


                    dataGridView_Result.Rows[rowCount - 1].Cells[7].Value = strResult;
                    dataGridView_Result.Rows[rowCount - 1].Cells[8].Value = Chanel.ToString();

                }
                else
                {
                    DataGridViewRow[] rows = new DataGridViewRow[9999];
                    for (int i = 1; i < 10000; i++)
                    {
                        rows[i - 1] = dataGridView_Result.Rows[i];
                    }

                    dataGridView_Result.Rows.Clear();
                    dataGridView_Result.Rows.AddRange(rows);

                    //最近一行数据
                    dataGridView_Result.Rows.Add();
                    rowCount = dataGridView_Result.Rows.Count;
                    dataGridView_Result.Rows[rowCount - 1].Cells[0].Value = totalNum;
                    dataGridView_Result.Rows[rowCount - 1].Cells[1].Value = time;
                    dataGridView_Result.Rows[rowCount - 1].Cells[2].Value = QrCode;
                    dataGridView_Result.Rows[rowCount - 1].Cells[3].Value = double.Parse(AA.ToString("0.000"));
                    dataGridView_Result.Rows[rowCount - 1].Cells[4].Value = double.Parse(BB.ToString("0.000"));
                    dataGridView_Result.Rows[rowCount - 1].Cells[5].Value = double.Parse(CC.ToString("0.000"));
                    dataGridView_Result.Rows[rowCount - 1].Cells[6].Value = double.Parse(DD.ToString("0.000"));
                    dataGridView_Result.Rows[rowCount - 1].Cells[7].Value = strResult;
                    dataGridView_Result.Rows[rowCount - 1].Cells[8].Value = Chanel.ToString();

                }

                rowCount = dataGridView_Result.Rows.Count;
                dataGridView_Result.CurrentCell = dataGridView_Result.Rows[rowCount - 1].Cells[0];
                //dataGridView_Result.CurrentRow.Selected = true;
            }
            catch (Exception ex)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Src"></param>
        /// <param name="FileType">文件夹类型：0,NG;1,OK;2,NU;3,Test</param>
        /// <param name="Name"></param>
        /// 
        public static void WritePicture(HObject Dst, string FoldName, string FileName = "default",
            string Format = "jpg", bool IsResultImage = false)
        {
            try
            {
                if (IsResultImage) Format = "jpg";   //结果图像始终压缩

                DateTime T = DateTime.Now;
                if (FileName == "default") FileName = T.ToString("yyyyMMddHHmmss") + T.Millisecond.ToString();  //默认按时间保存
                string ParentPicPath = Directory.GetParent(Application.StartupPath).FullName;
                string fileName = DateTime.Now.ToLongDateString();
                string PartPicPath = ParentPicPath.Substring(0, 2) + "\\Image610\\" + FoldName + "\\";

                string PicPath = PartPicPath + fileName;
                if (!Directory.Exists(PicPath))
                {
                    Directory.CreateDirectory(PicPath);
                }

                //检查存储空间
                if (GetHardDiskFreeSpace(AppDomain.CurrentDomain.BaseDirectory.Substring(0, 1)) < 1.0)
                {
                    string[] directorys = Directory.GetDirectories(PartPicPath);
                    g_iMachineState = (int)MachineState.Stop;
                    MessageBox.Show(AppDomain.CurrentDomain.BaseDirectory.Substring(0, 1) +
                        "盘空间不足1G, 请备份后再开启运行");
                    //return;

                    //自动清
                    //if (directorys.Length > 1)
                    //{
                    //    Directory.Delete(directorys[0], true);
                    //    //throw new Exception("磁盘空间不足,已自动清理");
                    //}
                    //else if (directorys.Length == 1)
                    //{
                    //    string[] files = Directory.GetFiles(directorys[0]);
                    //    if (files.Length > 0)
                    //    {
                    //        File.Delete(files[0]);
                    //        //throw new Exception("磁盘空间不足,请手动清理");
                    //    }
                    //}

                    PicPath = PicPath + "\\" + FileName + "." + Format;
                }
                else
                {
                    PicPath = PicPath + "\\" + FileName + "." + Format;
                }

                if (IsResultImage)
                {
                    //结果图像压缩为jpeg 20格式
                    HOperatorSet.WriteImage(Dst, "jpeg 20", 0, PicPath);
                }
                else
                {
                    HOperatorSet.WriteImage(Dst, Format, 0, PicPath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(LogWriter.ErrCode(ex.StackTrace, "05") + ex.Message);
            }
        }

        public static long GetHardDiskFreeSpace(string str_HardDiskName)
        {
            long freeSpace = new long();
            str_HardDiskName = str_HardDiskName + ":\\";
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                if (drive.Name == str_HardDiskName)
                {
                    freeSpace = drive.TotalFreeSpace / (1024 * 1024 * 1024);
                }
            }
            return freeSpace;
        }

        private string CountInspectResult(HTuple CodeDateString, HTuple Lengthes)
        {
            string ret = "C";
            inspectResult[g_iProdSeri].Qrcode = "000000";
            inspectResult[g_iProdSeri].g_dAALength = (-1).ToString();
            inspectResult[g_iProdSeri].g_dBBLength = (-1).ToString();
            inspectResult[g_iProdSeri].g_dCCLength = (-1).ToString();
            inspectResult[g_iProdSeri].g_dDDLength = (-1).ToString();
            bool isNGB = false, isNGC = false;

            try
            {
                if (CodeDateString == "000000")
                {
                    ret = "C";
                    InsertRunMsg("*****************二维码识别失败！********* ", false);

                    isNGC = true;
                }
                else if (Lengthes.Length != 4)
                {
                    InsertRunMsg("测量线应该 =4 " + ",实际=" + Lengthes.Length.ToString(), false);
                    ret = "B";
                    isNGB = true;
                }
                else
                {
                    // InsertRunMsg("AOI Data:  " + Lengthes[0] + "  " + Lengthes[1] + "  " + Lengthes[2] + "  " + Lengthes[3], false);

                    for (int i = 0; i <= Lengthes.Length - 1; i++)
                    {
                        if (Lengthes[i] < 40) Lengthes[i] = -1;
                        if ((double)FormProdctPar.UpDown_upperDeviation.Value < (Lengthes[i] - (double)FormProdctPar.UpDown_DiaStd.Value))
                        {
                            ret = "B";
                            isNGB = true;
                            break;
                        }
                        else if (-(double)FormProdctPar.UpDown_lowerDeviation.Value > (Lengthes[i] - (double)FormProdctPar.UpDown_DiaStd.Value))
                        {
                            ret = "C";
                            isNGC = true;
                            break;
                        }
                        else
                        {
                            ret = "A";
                        }

                    }
                    for (int i = 0; i <= Lengthes.Length - 1; i++)
                    {

                        if (i == 0) inspectResult[g_iProdSeri].g_dAALength = Lengthes[i].D.ToString("#0.0000");
                        if (i == 1) inspectResult[g_iProdSeri].g_dBBLength = Lengthes[i].D.ToString("#0.0000");
                        if (i == 2) inspectResult[g_iProdSeri].g_dCCLength = Lengthes[i].D.ToString("#0.0000");
                        if (i == 3) inspectResult[g_iProdSeri].g_dDDLength = Lengthes[i].D.ToString("#0.0000");
                    }
                }
                g_iTotal++;
                g_iCountUph++;
                if (isNGB || isNGC)
                {
                    g_iNgCount++;
                    if (isNGB) g_iisNGBCount++;
                    if (isNGC) g_iisNGCCount++;
                }
                else
                {
                    g_iOkCount++;
                }

                //刷新界面上的检测结果
                double tmpOK = Convert.ToDouble(g_iOkCount.ToString("#0.00"));
                double tmpTotal = Convert.ToDouble(g_iTotal.ToString("#0.00"));

                textBox_A_rate.Text = ((tmpOK / tmpTotal) * 100.00).ToString("#0.00") + " %";
                textBox_A.Text = g_iOkCount.ToString();
                textBox_B.Text = g_iisNGBCount.ToString();
                textBox_C.Text = g_iisNGCCount.ToString();
                textBox_ng_count.Text = g_iNgCount.ToString();
                textBox_total.Text = g_iTotal.ToString();

                return ret;
            }
            catch (Exception ex)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
            }

            return ret;
        }

        private static void ShowErrorMsg(string msg)
        {
            formMainDelegate("DispErroMsg", msg);
        }

        public static bool DeleteDirSafely(HTuple hv_Dir)
        {
            bool ret = false;

            // Local control variables 
            HTuple hv_Files = new HTuple(), hv_Index = new HTuple();
            HTuple hv_strLen = new HTuple(), hv_Pos = new HTuple();
            HTuple hv_Pos1 = new HTuple(), hv_Exception = null, hv_FilesExist = new HTuple();

            try
            {
                HOperatorSet.FileExists(hv_Dir, out hv_FilesExist);
                if (hv_FilesExist < 1) { return ret; }

                HOperatorSet.ListFiles(hv_Dir, (new HTuple("files")).TupleConcat("directories"), out hv_Files);
                for (hv_Index = 0; (int)hv_Index <= (int)((new HTuple(hv_Files.TupleLength())) - 1); hv_Index = (int)hv_Index + 1)
                {
                    hv_strLen = ((hv_Files.TupleSelect(hv_Index))).TupleStrlen();
                    hv_Pos = ((hv_Files.TupleSelect(hv_Index))).TupleStrrchr(".");
                    if ((int)(new HTuple((new HTuple(-1)).TupleEqual(hv_Pos))) != 0)
                    {
                        hv_Pos1 = ((hv_Files.TupleSelect(hv_Index))).TupleStrrchr("\\");
                        if ((int)(new HTuple(((hv_Pos1 + 1)).TupleEqual(hv_strLen))) != 0)
                        {
                            //记得添加自己到函数目录，否则无法加载自身
                            DeleteDirSafely(hv_Files.TupleSelect(hv_Index));
                        }
                    }
                    else
                    {
                        HOperatorSet.DeleteFile(hv_Files.TupleSelect(hv_Index));
                    }
                }
                HOperatorSet.RemoveDir(hv_Dir);
                ret = true;
            }
            catch (HalconException CvEx)
            {
                CvEx.ToHTuple(out hv_Exception);
            }

            return ret;
        }

        private void SendloadResult2Plc1(int suckerIndex, int ret, int Xoff, int Yoff, int Uoff)
        {
            //PLC最大接受 65525

            string X_Daddr = string.Empty;
            string Y_Daddr = string.Empty;
            string U_Daddr = string.Empty;
            switch (suckerIndex)
            {
                case 1:
                    X_Daddr = "D2100=";
                    Y_Daddr = "D2102=";
                    U_Daddr = "D2104=";
                    break;
                case 2:
                    X_Daddr = "D2106=";
                    Y_Daddr = "D2108=";
                    U_Daddr = "D2110=";
                    break;
                case 3:
                    X_Daddr = "D2112=";
                    Y_Daddr = "D2114=";
                    U_Daddr = "D2116=";
                    break;
                case 4:
                    X_Daddr = "D2118=";
                    Y_Daddr = "D2120=";
                    U_Daddr = "D2122=";
                    break;
                case 5:
                    X_Daddr = "D2124=";
                    Y_Daddr = "D2126=";
                    U_Daddr = "D2128=";
                    break;
                case 6:
                    X_Daddr = "D2130=";
                    Y_Daddr = "D2132=";
                    U_Daddr = "D2134=";
                    break;
                default:
                    break;
            }

            SetMcPlc(X_Daddr + Xoff.ToString(), PLC1);                 //发送偏移量
            SetMcPlc(Y_Daddr + Yoff.ToString(), PLC1);                 //发送偏移量
            SetMcPlc(U_Daddr + (Uoff + 2000).ToString(), PLC1);                //发送偏移量   
                                                                               //SetMcPlc(X_Daddr + "0", PLC1);                       //发送偏移量
                                                                               //SetMcPlc(Y_Daddr + "0", PLC1);                      //发送偏移量
                                                                               //SetMcPlc(U_Daddr + "0", PLC1);                     //发送偏移量

            InsertRunMsg("  Offset Sended  " + suckerIndex + "U:" + (Uoff + 2000).ToString(), false);
        }

        private void SendAOIResult2Plc3(int StatioIndex, string strABC)
        {
            //OK  1  NG 2
            InsertRunMsg(StatioIndex + "工位发送结果(OK:1  NG:2):  " + strABC, false);
            string Daddr = string.Empty;
            int result = 1;
            switch (StatioIndex)
            {
                case 1:
                    Daddr = "D2100=";
                    break;
                case 2:
                    Daddr = "D2101=";
                    break;
                case 3:
                    Daddr = "D2102=";
                    break;

                default:
                    break;
            }

            switch (strABC)
            {
                case "A":
                    result = 1;

                    break;
                case "B":
                    result = 2;
                    break;
                case "C":
                    result = 2;
                    break;

                default:
                    break;
            }

            SetMcPlc(Daddr + result.ToString(), PLC3);                 //发送结果

            if (StatioIndex == 1)
            {
                SetMcPlc("D2110 =1", PLC3);    //结果全部发送后  Flag 置1  

                _sw2.Stop();
                TimeSpan ts = _sw2.Elapsed;
                double Ct = ts.TotalMilliseconds;
                textBox_CT.Text = (Ct / 3000).ToString("#0.00") + "s";
                _sw2.Restart();
            }



        }
        //private void GetImageFromCam0Todo(HObject img)
        //{
        //    if (img == null)
        //    {
        //        return;
        //    }
        //    InsertRunMsg("Load -- Cam0 Get Image  ", false);
        //    GetMcPlc("D2151 ", PLC1, out List<String> McData2151);   //吸嘴编号
        //    int suckerIndex = int.Parse(McData2151[0]);            //当前吸嘴编号          
        //    Cam0ImageList.Add(suckerIndex, img);
        //    InsertRunMsg("Load --- " + suckerIndex + "  Cam0 Get Image,   IMG NUM:  " + Cam0ImageList.Count, false);
        //    if (g_iMachineState == (int)MachineState.Stop)
        //    {
        //        ho_SrcImg[3] = img;
        //    }

        //}
        private void GetImageFromCam0Todo(HObject img)
        {
            if (img == null)
            {
                return;
            }
            if (g_iMachineState == (int)MachineState.Stop)
            {
                FormMain.ho_SrcImg[3] = img;
                hWin0.DisPlay(ho_SrcImg[3], null, string.Empty);
                return;
            }

            InsertRunMsg("Load -- Cam0 Get Image  ", false);
            Cam0Imagequeue.Enqueue(img);


        }

        private void GetImageFromCam1Todo(HObject img1)
        {

            if (img1 == null)
            {
                return;
            }

            InsertRunMsg("AOI -- Cam1 Get Image  ", false);
            Cam1Imagequeue.Enqueue(img1);

            //InsertRunMsg("AOI -- stationIndex:  " + Cam1ImageIndex + "   Cam1ImageList.Count=: " + Cam1ImageList.Count, false);                    
        }

        #endregion

        #region 线程
        //Timer
        private void Timer_CountUph_Tick(object sender, EventArgs e)
        {
            AddUphPoint();

            g_iCountUph = 0;
        }

        private void timer_CheckDoneAxis_0_Tick(object sender, EventArgs e)
        {
            if (PLC1 != null)
            {

            }



        }
        //Thread
        private void InitDevice()
        {
            if (g_bInitDevice)
            {
                Control.CheckForIllegalCrossThreadCalls = false;    //这个类中我们不检查跨线程的调用是否合法
                g_bInitDevice = false;

                #region 初始化相机

                InitCam();

                #endregion

                #region 连接PLC 、加载标定文件
                ConnectPlc1();    //上料PLC
                ConnectPlc2();    //上料PLC
                ConnectPlc3();    //下料PLC

                SetMcPlc("D2151 =0", PLC1);                 //吸嘴置0
                SetMcPlc("D2120=0", PLC2);                 //AOI拍照 置0



                LoadCalibData();
                #endregion


                if (!g_bCam0Open)
                {
                    MessageBox.Show("0#相机连接失败！", "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                if (!g_bCam1Open)
                {
                    MessageBox.Show("1#相机连接失败！", "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                if (PLC1 == null)
                {
                    MessageBox.Show("1# PLC连接失败！", "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                if (PLC2 == null)
                {
                    MessageBox.Show("2# PLC连接失败！", "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                if (PLC3 == null)
                {
                    MessageBox.Show("3# PLC连接失败！", "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                InsertRunMsg("启动完成..........单击 AUTO RUN 开始工作 ..", false);
                InitFace();   //部分控件需刷卡认证使能
            }

        }

        private void ContinueGrabCam0(object obj)
        {
            while (true)
            {
                Thread.Sleep(10);

                while (g_bCam0ContinueGrab)
                {
                    if (CamDevice[hCam0].TrigImage != null) CamDevice[hCam0].TrigImage.Dispose();
                    CamDevice[hCam0].TrigImage = new HObject();   //图像复位
                    CamDevice[hCam0].SetTriggerMode(7); //软触发
                    CamDevice[hCam0].SoftTriggerExec();

                    ho_SrcImg[0] = CamDevice[hCam0].TrigImage.Clone();
                    hWin0.DisPlay(ho_SrcImg[0].Clone(), null, string.Empty);
                }
            }
        }

        private void ContinueGrabCam1(object obj)
        {
            while (true)
            {
                Thread.Sleep(10);

                while (g_bCam1ContinueGrab)
                {
                    if (CamDevice[hCam1].TrigImage != null) CamDevice[hCam1].TrigImage.Dispose();
                    CamDevice[hCam1].TrigImage = new HObject();   //图像复位
                    CamDevice[hCam1].SetTriggerMode(7); //软触发
                    CamDevice[hCam1].SoftTriggerExec();
                    ho_SrcImg[1] = CamDevice[hCam1].TrigImage.Clone();
                    hWin1.DisPlay(ho_SrcImg[1].Clone(), null, string.Empty);
                }
            }
        }

        //private void LoadCap()   //获取D  OK 
        //{
        //    int suckerIndex = 0; //吸嘴编号
        //    while (true)
        //    {
        //        Thread.Sleep(2);
        //        // InsertRunMsg("--> Load", false);

        //        while (g_iMachineState == (int)MachineState.Run)
        //        {
        //            int waitTimes0 = 0;

        //            if (Cam0ImageList.Count > 0)
        //            {
        //                InsertRunMsg("  in Load   ", false);
        //                waitTimes0 = 0;

        //                ho_SrcImg[0] = Cam0ImageList.Values[0].Clone();
        //                suckerIndex = Cam0ImageList.Keys[0];

        //                _single_AOIPP.Stop();
        //                int Index = 0;
        //                for (int i = 0; i < 6; i++)
        //                {
        //                    if (Cam0ImageList.Values[0] == null)
        //                    {
        //                        Index = i;
        //                        Thread.Sleep(5);
        //                    }
        //                    else
        //                    {
        //                        break;
        //                    }
        //                }
        //                if (Index == 5 && Cam0ImageList.Values[0] == null)
        //                {
        //                    Cam0ImageList.RemoveAt(0);
        //                    continue;
        //                }
        //                try
        //                {
        //                    //InsertRunMsg(Cam0ImageList.Keys[0]+ "  in Cam0Detect   ", false);
        //                    Cam0Detect(ho_SrcImg[0], suckerIndex, out HTuple hv_CodeDateString, out HObject outLoadResultImage,
        //                        out double Xoff, out double Yoff, out double Uoff);

        //                    if (Cam0ImageList.Values[0] != null) { Cam0ImageList.Values[0].Dispose(); }
        //                    Cam0ImageList.RemoveAt(0);

        //                    lock (LockWriteSeri)
        //                    {
        //                        if (Math.Abs(Math.Abs(Yoff) - 999999) < 0.5 && Math.Abs(Math.Abs(Xoff) - 999999) < 0.5)
        //                        {
        //                            inspectResult[suckerIndex].g_dXOff = 0;
        //                            inspectResult[suckerIndex].g_dYoff = 0;
        //                            inspectResult[suckerIndex].g_dangleoff = 0;
        //                            inspectResult[suckerIndex].bLoadRet = 2;    //1  ok    2ng
        //                            InsertRunMsg(suckerIndex + "定位NG**********", false);
        //                        }
        //                        else
        //                        {
        //                            //if (Xoff > 10000 || Yoff > 10000) InsertRunMsg("偏移超过极限", false);
        //                            inspectResult[suckerIndex].g_dXOff = (int)Xoff;
        //                            inspectResult[suckerIndex].g_dYoff = (int)Yoff;
        //                            inspectResult[suckerIndex].g_dangleoff = (int)(Uoff * 5.55555);   //U轴 2000个脉冲一转 
        //                            inspectResult[suckerIndex].bLoadRet = 1;    //1  ok    2ng
        //                        }
        //                    }

        //                    SendloadResult2Plc1(suckerIndex, inspectResult[suckerIndex].bLoadRet, inspectResult[suckerIndex].g_dXOff, inspectResult[suckerIndex].g_dYoff, inspectResult[suckerIndex].g_dangleoff);


        //                    if (suckerIndex == 1 || suckerIndex == 4)
        //                    {

        //                        SetMcPlc("D2150=1", PLC1);                                   //置1
        //                    }

        //                }
        //                catch (HalconException CvEx)
        //                {
        //                    Cam0ImageList.RemoveAt(0);
        //                    LogWriter.WriteLog(LogWriter.ErrCode(CvEx.StackTrace, "06") + CvEx.Message);
        //                    InsertRunMsg("LoadCap抛出异常*****", false);
        //                    inspectResult[Cam0ImageList.Keys[0]].g_dXOff = 0;
        //                    inspectResult[Cam0ImageList.Keys[0]].g_dYoff = 0;
        //                    inspectResult[Cam0ImageList.Keys[0]].g_dangleoff = 0;
        //                    inspectResult[Cam0ImageList.Keys[0]].bLoadRet = 2;    //1  ok    2ng
        //                    SendloadResult2Plc1(suckerIndex, inspectResult[suckerIndex].bLoadRet, inspectResult[suckerIndex].g_dXOff, inspectResult[suckerIndex].g_dYoff, inspectResult[suckerIndex].g_dangleoff);
        //                }
        //                catch (Exception ex)
        //                {
        //                    Cam0ImageList.RemoveAt(0);
        //                    LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
        //                    InsertRunMsg("LoadCap抛出异常*****", false);
        //                    inspectResult[Cam0ImageList.Keys[0]].g_dXOff = 0;
        //                    inspectResult[Cam0ImageList.Keys[0]].g_dYoff = 0;
        //                    inspectResult[Cam0ImageList.Keys[0]].g_dangleoff = 0;
        //                    inspectResult[Cam0ImageList.Keys[0]].bLoadRet = 2;    //1  ok    2ng
        //                    SendloadResult2Plc1(suckerIndex, inspectResult[suckerIndex].bLoadRet, inspectResult[suckerIndex].g_dXOff, inspectResult[suckerIndex].g_dYoff, inspectResult[suckerIndex].g_dangleoff);
        //                }
        //            }
        //            else
        //            {
        //                if (waitTimes0 == 0)
        //                {
        //                    // InsertRunMsg("等待获取照片...", false);
        //                }
        //            }
        //            Thread.Sleep(20);
        //        }
        //    }
        //}
        private void LoadCap()   //不交互 、队列
        {
            int suckerIndex = 0; //吸嘴编号
            while (true)
            {
                Thread.Sleep(2);
                while (g_iMachineState == (int)MachineState.Run)
                {
                    if (Cam0Imagequeue.Count > 0)
                    {
                        InsertRunMsg("  in Load   ", false);
                        Cam0ImageIndex = Cam0ImageIndex - 1;

                        ho_SrcImg[0] = Cam0Imagequeue.Dequeue();
                        suckerIndex = Cam0ImageIndex;
                        //int Index = 0;
                        //for (int i = 0; i < 6; i++)
                        //{
                        //    if (Cam0ImageList.Values[0] == null)
                        //    {
                        //        Index = i;
                        //        Thread.Sleep(5);
                        //    }
                        //    else
                        //    {
                        //        break;
                        //    }
                        //}
                        //if (Index == 5 && Cam0ImageList.Values[0] == null)
                        //{
                        //    Cam0ImageList.RemoveAt(0);
                        //    continue;
                        //}
                        try
                        {
                            //InsertRunMsg(Cam0ImageList.Keys[0]+ "  in Cam0Detect   ", false);
                            Cam0Detect(ho_SrcImg[0], suckerIndex, out HTuple hv_CodeDateString, out HObject outLoadResultImage,
                                out double Xoff, out double Yoff, out double Uoff);

                            lock (LockWriteSeri)
                            {
                                if (Math.Abs(Math.Abs(Yoff) - 999999) < 0.5 && Math.Abs(Math.Abs(Xoff) - 999999) < 0.5)
                                {
                                    inspectResult[suckerIndex].g_dXOff = 0;
                                    inspectResult[suckerIndex].g_dYoff = 0;
                                    inspectResult[suckerIndex].g_dangleoff = 0;
                                    inspectResult[suckerIndex].bLoadRet = 2;    //1  ok    2ng
                                    InsertRunMsg(suckerIndex + "定位NG**********", false);
                                }
                                else
                                {
                                    //if (Xoff > 10000 || Yoff > 10000) InsertRunMsg("偏移超过极限", false);
                                    inspectResult[suckerIndex].g_dXOff = (int)Xoff;
                                    inspectResult[suckerIndex].g_dYoff = (int)Yoff;
                                    inspectResult[suckerIndex].g_dangleoff = (int)(Uoff * 5.55555);   //U轴 2000个脉冲一转 
                                    inspectResult[suckerIndex].bLoadRet = 1;    //1  ok    2ng
                                }
                            }

                            SendloadResult2Plc1(suckerIndex, inspectResult[suckerIndex].bLoadRet, inspectResult[suckerIndex].g_dXOff,
                                inspectResult[suckerIndex].g_dYoff, inspectResult[suckerIndex].g_dangleoff);

                            if (suckerIndex == 1 || suckerIndex == 4)
                            {
                                SetMcPlc("D2150=1", PLC1);                                   //置1
                            }

                        }
                        catch (HalconException CvEx)
                        {

                            LogWriter.WriteLog(LogWriter.ErrCode(CvEx.StackTrace, "06") + CvEx.Message);
                            InsertRunMsg("LoadCap抛出异常*****", false);
                            inspectResult[Cam0ImageIndex].g_dXOff = 0;
                            inspectResult[Cam0ImageIndex].g_dYoff = 0;
                            inspectResult[Cam0ImageIndex].g_dangleoff = 0;
                            inspectResult[Cam0ImageIndex].bLoadRet = 2;    //1  ok    2ng
                            SendloadResult2Plc1(suckerIndex, inspectResult[suckerIndex].bLoadRet, inspectResult[suckerIndex].g_dXOff, inspectResult[suckerIndex].g_dYoff, inspectResult[suckerIndex].g_dangleoff);
                        }
                        catch (Exception ex)
                        {

                            LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
                            InsertRunMsg("LoadCap抛出异常*****", false);
                            inspectResult[Cam0ImageIndex].g_dXOff = 0;
                            inspectResult[Cam0ImageIndex].g_dYoff = 0;
                            inspectResult[Cam0ImageIndex].g_dangleoff = 0;
                            inspectResult[Cam0ImageIndex].bLoadRet = 2;    //1  ok    2ng
                            SendloadResult2Plc1(suckerIndex, inspectResult[suckerIndex].bLoadRet, inspectResult[suckerIndex].g_dXOff, inspectResult[suckerIndex].g_dYoff, inspectResult[suckerIndex].g_dangleoff);
                        }
                        if (Cam0ImageIndex == 1) Cam0ImageIndex = 7;
                    }
                    else
                    {

                    }
                    Thread.Sleep(20);
                }
            }
        }

        private void AOI()   //AOI检测工位
        {
            while (true)
            {
                Thread.Sleep(2);
                while (g_iMachineState == (int)MachineState.Run)
                {
                    _single_AOIPP.Restart();
                    if (Cam1Imagequeue.Count > 0)
                    {
                        Thread.Sleep(5);
                        Cam1ImageIndex = Cam1ImageIndex - 1;
                        InsertRunMsg(" -1- in AOI--", false);
                        g_iProdSeri++;
                        g_iProdCT++;

                        inspectResult[g_iProdSeri].channel = Cam1ImageIndex;
                        ho_SrcImg[1] = Cam1Imagequeue.Dequeue();

                        //InsertRunMsg(" -2- in AOI--", false);

                        _single_AOIPP.Stop();


                        _single_GrabAOI.Restart();
                        string strResult = "A";
                        try
                        {
                            //AOI检测算法      OK 1  NG  2  
                            StartTimeAOI = DateTime.Now;
                            //InsertRunMsg(" -3- in AOI--", false);

                            InsertRunMsg("AOI--" + inspectResult[g_iProdSeri].channel + "channel  image in AOI Cam1Detect()", false);

                            Cam1Detect(ho_SrcImg[1].Clone(), inspectResult[g_iProdSeri].channel, Convert.ToInt32(FormProdctPar.cmb_MultiLine.Text),
                                Convert.ToDouble(FormProdctPar.numericUpDown_MultiAngle.Value),
                                 Convert.ToDouble(FormProdctPar.NDU_AAoff.Value), Convert.ToDouble(FormProdctPar.NDU_BBoff.Value),
                                Convert.ToDouble(FormProdctPar.NDU_CCoff.Value), Convert.ToDouble(FormProdctPar.NDU_DDoff.Value),
                                out HTuple CodeDateString, out HObject resultImage, out HTuple Lengthes);

                            //InsertRunMsg(" -4- in AOI--", false);
                            //if (Cam1ImageList.Values[0] != null) { Cam1ImageList.Values[0].Dispose(); }
                            //Cam1ImageList.RemoveAt(0);
                            //InsertRunMsg(" -5- in AOI--", false);

                            lock (LockWriteResult)
                            {
                                //判断输出结果类别                               
                                strResult = CountInspectResult(CodeDateString, Lengthes);
                                // InsertRunMsg(" -5- in AOI--", false);
                                SendAOIResult2Plc3(inspectResult[g_iProdSeri].channel, strResult);

                                inspectResult[g_iProdSeri].strInspectResult = strResult;
                                inspectResult[g_iProdSeri].Qrcode = CodeDateString;

                                inspectResult[g_iProdSeri].bWriteRecord = false;
                                inspectResult[g_iProdSeri].Cam1ProcDone = 1;
                            }
                            SpanTimeAOI = DateTime.Now - StartTimeAOI;
                            ElspseTimeAOI = SpanTimeAOI.TotalMilliseconds;

                            HWin hWinID = hWin1;
                            if (inspectResult[g_iProdSeri].channel == 1)
                            {
                                lab_AOITime1.Text = ElspseTimeAOI.ToString() + "ms";
                                hWinID = hWin1;
                            }
                            if (inspectResult[g_iProdSeri].channel == 2)
                            {
                                lab_AOITime2.Text = ElspseTimeAOI.ToString() + "ms";
                                hWinID = hWin2;
                            }
                            if (inspectResult[g_iProdSeri].channel == 3)
                            {
                                lab_AOITime3.Text = ElspseTimeAOI.ToString() + "ms";
                                hWinID = hWin3;
                            }
                            HObject ho_DispImg = resultImage.Clone();
                            hWinID.DisPlay(ho_DispImg, null, strResult);
                            // InsertRunMsg(" -7- in AOI--", false);
                            if (FormProdctPar.checkBox_save_ok_image_cam1.Checked)
                            {

                                saveImage(CodeDateString, resultImage.Clone(), 1, 0, inspectResult[g_iProdSeri].channel);
                            }
                            if (FormProdctPar.checkBox_save_origion_image_cam1.Checked)
                            {
                                saveImage(CodeDateString, ho_SrcImg[1], 1, 0, inspectResult[g_iProdSeri].channel);

                            }
                            InsertRunMsg("AOI--" + " -->processed , Have Image  " + Cam1Imagequeue.Count, false);

                            // InsertRunMsg("AOI--" + " -->processed , Have Image  " + Cam1ImageList.Count, false);

                            if (Cam1ImageIndex == 1) Cam1ImageIndex = 4;





                            resultImage.Dispose();
                        }
                        catch (HalconException CvEx)
                        {
                            //Cam1ImageList.RemoveAt(0);
                            LogWriter.WriteLog(LogWriter.ErrCode(CvEx.StackTrace, "06") + CvEx.Message);
                        }
                        catch (Exception ex)
                        {
                            //Cam1ImageList.RemoveAt(0);
                            LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
                        }

                    }
                    else
                    {

                    }
                    Thread.Sleep(20);
                }
            }
        }

        private void ScanMCD()
        {
            while (true)
            {
                Thread.Sleep(500);

                GetMcPlc("D1006 ", PLC3, out List<String> McDataD1006);
                if (McDataD1006[0] != "1")
                {
                    InsertRunMsg("__急停被触发__ ", false);
                    textBox_ErrorMsg.Text = "急停被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;
                    }

                }
                if (McDataD1006[0] != "2")
                {
                    InsertRunMsg("OK空tray顶升轴故障 ", false);
                    textBox_ErrorMsg.Text = "OK空tray顶升轴故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;
                    }
                }
                if (McDataD1006[0] != "3")
                {
                    InsertRunMsg("OK空tray顶升轴故障 ", false);
                    textBox_ErrorMsg.Text = "OK空tray顶升轴故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;
                    }
                }
                if (McDataD1006[0] != "4")
                {
                    InsertRunMsg("OK空tray顶升轴上限位被触发", false);
                    textBox_ErrorMsg.Text = "OK空tray顶升轴上限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "5")
                {
                    InsertRunMsg("OK空tray顶升轴下限位被触发", false);
                    textBox_ErrorMsg.Text = "OK空tray顶升轴下限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "6")
                {
                    InsertRunMsg("OK满tray顶升轴故障", false);
                    textBox_ErrorMsg.Text = "OK满tray顶升轴故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "7")
                {
                    InsertRunMsg("OK满tray顶升轴上限位被触发 ", false);
                    textBox_ErrorMsg.Text = "OK满tray顶升轴上限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "8")
                {
                    InsertRunMsg("OK满tray顶升轴下限位被触发 ", false);
                    textBox_ErrorMsg.Text = "OK满tray顶升轴下限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "9")
                {
                    InsertRunMsg("NG空tray顶升轴故障 ", false);
                    textBox_ErrorMsg.Text = "NG空tray顶升轴故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "10")
                {
                    InsertRunMsg("NG空tray顶升轴上限位被触发 ", false);
                    textBox_ErrorMsg.Text = "NG空tray顶升轴上限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "11")
                {
                    InsertRunMsg("NG空tray顶升轴下限位被触发", false);
                    textBox_ErrorMsg.Text = "NG空tray顶升轴下限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "12")
                {
                    InsertRunMsg("NG满tray顶升轴故障 ", false);
                    textBox_ErrorMsg.Text = "NG满tray顶升轴故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "13")
                {
                    InsertRunMsg("NG满tray顶升轴上限位被触发 ", false);
                    textBox_ErrorMsg.Text = "NG满tray顶升轴上限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "14")
                {
                    InsertRunMsg("NG满tray顶升轴下限位被触发 ", false);
                    textBox_ErrorMsg.Text = "NG满tray顶升轴下限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                if (McDataD1006[0] != "15")
                {
                    InsertRunMsg("OK堆垛升降轴故障 ", false);
                    textBox_ErrorMsg.Text = "OK堆垛升降轴故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        return;

                    }
                }
                ////////////////////////////////////////////
                GetMcPlc("D1007 ", PLC3, out List<String> McDataD1007);
                if (McDataD1007[0] != "0")
                {
                    InsertRunMsg("OK堆垛升降轴上限位被触发", false);
                    textBox_ErrorMsg.Text = "OK堆垛升降轴上限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1007[0] != "1")
                {
                    InsertRunMsg("OK堆垛升降轴下限位被触发", false);
                    textBox_ErrorMsg.Text = "OK堆垛升降轴下限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       

                    }
                }
                if (McDataD1007[0] != "2")
                {
                    InsertRunMsg("NG堆垛升降轴故障", false);
                    textBox_ErrorMsg.Text = "NG堆垛升降轴故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       

                    }
                }
                if (McDataD1007[0] != "3")
                {
                    InsertRunMsg("NG堆垛升降轴上限位被触发", false);
                    textBox_ErrorMsg.Text = "NG堆垛升降轴上限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       

                    }
                }
                if (McDataD1007[0] != "4")
                {
                    InsertRunMsg("NG堆垛升降轴下限位被触发", false);
                    textBox_ErrorMsg.Text = "NG堆垛升降轴下限位被触发";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       

                    }
                }
                if (McDataD1007[0] != "5")
                {
                    InsertRunMsg("下料机械手故障", false);
                    textBox_ErrorMsg.Text = "下料机械手故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       

                    }
                }
                if (McDataD1007[0] != "6")
                {
                    InsertRunMsg("OK空tray分料气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray分料气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       

                    }
                }
                if (McDataD1007[0] != "7")
                {
                    InsertRunMsg("OK空tray分料气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray分料气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       

                    }
                }
                if (McDataD1007[0] != "8")
                {
                    InsertRunMsg("OK空tray传输阻挡气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray传输阻挡气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       

                    }
                }
                if (McDataD1007[0] != "9")
                {
                    InsertRunMsg("OK空tray传输阻挡气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray传输阻挡气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1007[0] != "10")
                {
                    InsertRunMsg("OK空tray前端取料固定气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray前端取料固定气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1007[0] != "11")
                {
                    InsertRunMsg("OK空tray前端取料固定气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray前端取料固定气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1007[0] != "12")
                {
                    InsertRunMsg("OK空tray后端取料固定气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray后端取料固定气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1007[0] != "13")
                {
                    InsertRunMsg("OK空tray后端取料固定气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray后端取料固定气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                     
                    }
                }
                if (McDataD1007[0] != "14")
                {
                    InsertRunMsg("OK满tray分料气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "OK满tray分料气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                   
                    }
                }
                if (McDataD1007[0] != "15")
                {
                    InsertRunMsg("OK满tray分料气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "OK满tray分料气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                /////////////////////////////////////////////////////////////
                GetMcPlc("D1008 ", PLC3, out List<String> McDataD1008);
                if (McDataD1008[0] != "0")
                {
                    InsertRunMsg("OK空tray分料真空吸去工作位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray分料真空吸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "1")
                {
                    InsertRunMsg("OK空tray分料真空吸回原始位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray分料真空吸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "2")
                {
                    InsertRunMsg("NG空tray分料气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray分料气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "3")
                {
                    InsertRunMsg("NG空tray分料气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray分料气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "4")
                {
                    InsertRunMsg("NG空tray传输阻挡气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray传输阻挡气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        
                    }
                }
                if (McDataD1008[0] != "5")
                {
                    InsertRunMsg("NG空tray传输阻挡气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray传输阻挡气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "6")
                {
                    InsertRunMsg("NG空tray前端取料固定气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray前端取料固定气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "7")
                {
                    InsertRunMsg("NG空tray前端取料固定气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray前端取料固定气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "8")
                {
                    InsertRunMsg("NG空tray后端取料固定气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray后端取料固定气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "9")
                {
                    InsertRunMsg("NG空tray后端取料固定气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray后端取料固定气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "10")
                {
                    InsertRunMsg("NG满tray分料气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "NG满tray分料气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "11")
                {
                    InsertRunMsg("NG满tray分料气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "NG满tray分料气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        
                    }
                }
                if (McDataD1008[0] != "12")
                {
                    InsertRunMsg("NG空tray分料真空吸去工作位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray分料真空吸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        
                    }
                }
                if (McDataD1008[0] != "13")
                {
                    InsertRunMsg("NG空tray分料真空吸回原始位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray分料真空吸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        
                    }
                }
                if (McDataD1008[0] != "14")
                {
                    InsertRunMsg("OK空tray前端取料推紧气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray前端取料推紧气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1008[0] != "15")
                {
                    InsertRunMsg("OK空tray前端取料推紧气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray前端取料推紧气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                ////////////////////////////////////////////////////////
                GetMcPlc("D1009 ", PLC3, out List<String> McDataD1009);
                if (McDataD1009[0] != "0")
                {
                    InsertRunMsg("NG空tray前端取料推紧气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray前端取料推紧气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1009[0] != "1")
                {
                    InsertRunMsg("NG空tray前端取料推紧气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray前端取料推紧气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        
                    }
                }
                if (McDataD1009[0] != "2")
                {
                    InsertRunMsg("OK空tray后端取料推紧气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray后端取料推紧气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1009[0] != "3")
                {
                    InsertRunMsg("OK空tray后端取料推紧气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "OK空tray后端取料推紧气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        
                    }
                }
                if (McDataD1009[0] != "4")
                {
                    InsertRunMsg("NG空tray后端取料推紧气缸去工作位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray后端取料推紧气缸去工作位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1009[0] != "5")
                {
                    InsertRunMsg("NG空tray后端取料推紧气缸回原始位故障", false);
                    textBox_ErrorMsg.Text = "NG空tray后端取料推紧气缸回原始位故障";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                ///////////////////////////////////////////////////////
                GetMcPlc("D1010 ", PLC3, out List<String> McDataD1010);
                GetMcPlc("D1011 ", PLC3, out List<String> McDataD1011);
                GetMcPlc("D1012 ", PLC3, out List<String> McDataD1012);
                GetMcPlc("D1013 ", PLC3, out List<String> McDataD1013);
                ////////////////////////////////////////////////////////
                GetMcPlc("D1014 ", PLC3, out List<String> McDataD1014);
                if (McDataD1014[0] != "1")
                {
                    InsertRunMsg("OK空tray缺料报警", false);
                    textBox_ErrorMsg.Text = "OK空tray缺料报警";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1014[0] != "2")
                {
                    InsertRunMsg("OK空tray缺料停机报警", false);
                    textBox_ErrorMsg.Text = "OK空tray缺料停机报警";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1014[0] != "3")
                {
                    InsertRunMsg("OK满tray满料报警", false);
                    textBox_ErrorMsg.Text = "OK满tray满料报警";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1014[0] != "4")
                {
                    InsertRunMsg("OK满tray满料停机报警", false);
                    textBox_ErrorMsg.Text = "OK满tray满料停机报警";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1014[0] != "5")
                {
                    InsertRunMsg("NG空tray缺料报警", false);
                    textBox_ErrorMsg.Text = "NG空tray缺料报警";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }
                if (McDataD1014[0] != "6")
                {
                    InsertRunMsg("NG空tray缺料停机报警", false);
                    textBox_ErrorMsg.Text = "NG空tray缺料停机报警";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                        
                    }
                }
                if (McDataD1014[0] != "7")
                {
                    InsertRunMsg("NG满tray满料报警", false);
                    textBox_ErrorMsg.Text = "NG满tray满料报警";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                      
                    }
                }
                if (McDataD1014[0] != "8")
                {
                    InsertRunMsg("NG满tray满料停机报警", false);
                    textBox_ErrorMsg.Text = "NG满tray满料停机报警";
                    if (tabControl_DisplayResult.SelectedIndex != 2)
                    {
                        tabControl_DisplayResult.SelectedIndex = 2;
                       
                    }
                }

                GetMcPlc("D1015 ", PLC3, out List<String> McDataD1015);


            }
        }

        //private void SaveImage()
        //{
        //    while (true)
        //    {
        //        try
        //        {
        //            if (SaveNullImageList.Count > 0)
        //            {
        //                //检查图片的有效性
        //                int Index = 0;
        //                for (int i = 0; i < 10; i++)
        //                {
        //                    if (SaveNullImageList.Values[0] == null)
        //                    {
        //                        Index = i;
        //                        Thread.Sleep(5);
        //                    }
        //                    else
        //                    {
        //                        break;
        //                    }
        //                }
        //                if (Index == 9 && SaveNullImageList.Values[0] == null)
        //                {
        //                    SaveNullImageList.RemoveAt(0);
        //                    continue;
        //                }
        //                WritePicture(SaveNullImageList.Values[0], "NullSrc", "default", "bmp", false);
        //                if (SaveNullImageList.Values[0] != null) SaveNullImageList.Values[0].Dispose();
        //                SaveNullImageList.RemoveAt(0);

        //            }

        //            if (SaveOKImageList.Count > 0)
        //            {
        //                //检查图片的有效性
        //                int Index = 0;
        //                for (int i = 0; i < 10; i++)
        //                {
        //                    if (SaveOKImageList.Values[0] == null)
        //                    {
        //                        Index = i;
        //                        Thread.Sleep(5);
        //                    }
        //                    else
        //                    {
        //                        break;
        //                    }
        //                }
        //                if (Index == 9 && SaveOKImageList.Values[0] == null)
        //                {
        //                    SaveOKImageList.RemoveAt(0);
        //                    continue;
        //                }


        //                WritePicture(SaveOKImageList.Values[0], "OKSrc", "default", "bmp", false);

        //                if (SaveOKImageList.Values[0] != null) SaveOKImageList.Values[0].Dispose();
        //                SaveOKImageList.RemoveAt(0);

        //            }

        //            if (SaveNGImageList.Count > 0)
        //            {
        //                //检查图片的有效性
        //                int Index = 0;
        //                for (int i = 0; i < 10; i++)
        //                {
        //                    if (SaveNGImageList.Values[0] == null)
        //                    {
        //                        Index = i;
        //                        Thread.Sleep(5);
        //                    }
        //                    else
        //                    {
        //                        break;
        //                    }
        //                }
        //                if (Index == 9 && SaveNGImageList.Values[0] == null)
        //                {
        //                    SaveNGImageList.RemoveAt(0);
        //                    continue;
        //                }

        //                WritePicture(SaveNGImageList.Values[0], "NGSrc", "default", "bmp", false);

        //                if (SaveNGImageList.Values[0] != null) SaveNGImageList.Values[0].Dispose();
        //                SaveNGImageList.RemoveAt(0);

        //            }
        //            //GC.Collect();
        //        }
        //        catch (Exception ex)
        //        {
        //            LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "06") + ex.Message);
        //        }
        //    }
        //}

        //计算结果
        private void ShowandWriteResult()
        {

            while (true)
            {
                while (g_iMachineState == (int)MachineState.Run)
                {
                    // 
                    //InsertRunMsg("--> 2  CountResult()", false);
                    Thread.Sleep(0);
                    if (g_iProdSeri > 0 && inspectResult[g_iProdSeri].Cam1ProcDone == 1 && !inspectResult[g_iProdSeri].bWriteRecord)
                    {
                        inspectResult[g_iProdSeri].bWriteRecord = true;
                        inspectResult[g_iProdSeri].Cam1ProcDone = 0;

                        this.dataGridView_Result.Refresh();
                        if (this.dataGridView_Result.InvokeRequired)
                        {
                            this.dataGridView_Result.Invoke(new Action(() =>
                            {

                                ShowResultMsg(g_iTotal, DateTime.Now, inspectResult[g_iProdSeri].Qrcode,
                                            Convert.ToDouble(inspectResult[g_iProdSeri].g_dAALength),
                                            Convert.ToDouble(inspectResult[g_iProdSeri].g_dBBLength),
                                            Convert.ToDouble(inspectResult[g_iProdSeri].g_dCCLength),
                                            Convert.ToDouble(inspectResult[g_iProdSeri].g_dDDLength),
                                            inspectResult[g_iProdSeri].strInspectResult,
                                            inspectResult[g_iProdSeri].channel,
                                           false);
                                WriteCSV(g_iTotal);

                            }));
                        }
                        else
                        {
                            ShowResultMsg(g_iTotal, DateTime.Now, inspectResult[g_iProdSeri].Qrcode,
                                         Convert.ToDouble(inspectResult[g_iProdSeri].g_dAALength),
                                         Convert.ToDouble(inspectResult[g_iProdSeri].g_dBBLength),
                                         Convert.ToDouble(inspectResult[g_iProdSeri].g_dCCLength),
                                         Convert.ToDouble(inspectResult[g_iProdSeri].g_dDDLength),
                                        inspectResult[g_iProdSeri].strInspectResult,
                                        inspectResult[g_iProdSeri].channel,
                                       false);
                            WriteCSV(g_iTotal);
                        }

                        dataListg_iProdSeri.Add(g_iProdSeri);
                        if (Convert.ToDouble(inspectResult[g_iProdSeri].g_dAALength) >= 48.026 && Convert.ToDouble(inspectResult[g_iProdSeri].g_dAALength) <= 48.191)
                        {
                            dataListAA.Add(Convert.ToDouble(inspectResult[g_iProdSeri].g_dAALength));
                            dataListBB.Add(Convert.ToDouble(inspectResult[g_iProdSeri].g_dBBLength));
                            dataListCC.Add(Convert.ToDouble(inspectResult[g_iProdSeri].g_dCCLength));
                            dataListDD.Add(Convert.ToDouble(inspectResult[g_iProdSeri].g_dDDLength));
                            RefreshChart(dataListg_iProdSeri, dataListAA, dataListBB, dataListCC, dataListDD);
                        }
                        else
                        {
                            dataListAA.Add(48.125);
                            dataListBB.Add(48.125);
                            dataListCC.Add(48.125);
                            dataListDD.Add(48.125);
                            RefreshChart(dataListg_iProdSeri, dataListAA, dataListBB, dataListCC, dataListDD);
                        }
                    }
                }
            }
        }
        #endregion

        #region Delegate

        private void ProcessFormMaindDelegate(string Message, object data)
        {
            try
            {
                switch (Message)
                {
                    case "Run":
                        SetRun();
                        break;
                    case "DispErroMsg":
                        if ((string)data != string.Empty)
                        {
                            textBox_ErrorMsg.Text = (string)data;

                            if (tabControl_DisplayResult.SelectedIndex != 11)
                            {
                                tabControl_DisplayResult.SelectedIndex = 11;
                            }
                        }
                        else
                        {
                            if (tabControl_DisplayResult.SelectedIndex != 0)
                            {
                                tabControl_DisplayResult.SelectedIndex = 0;
                            }
                        }
                        break;
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ProcessProductDlgMsg(string Message, object data)
        {
            try
            {
                switch (Message)
                {
                    case "cam0GrabImage":
                        if (CamDevice[hCam0].TrigImage != null) CamDevice[hCam0].TrigImage.Dispose();
                        CamDevice[hCam0].TrigImage = new HObject();   //图像复位                        
                        CamDevice[hCam0].SetParam((float)decimal.ToDouble(FormProdctPar.UpDown_Cam0_ExposureTime.Value));

                        CamDevice[hCam0].SetTriggerMode(7); //软触发
                        CamDevice[hCam0].SoftTriggerExec();

                        ho_SrcImg[0] = CamDevice[hCam0].TrigImage.Clone();

                        hWin0.DisPlay(ho_SrcImg[0].Clone(), null, string.Empty);

                        InsertRunMsg("0#相机已成功获取图片.", false);
                        mainDlgSendToProductDlg("mainDlgGrabDoneCam0", "");
                        break;


                    case "cam1GrabImage":
                        if (CamDevice[hCam1].TrigImage != null) CamDevice[hCam1].TrigImage.Dispose();
                        CamDevice[hCam1].TrigImage = new HObject();   //图像复位                     
                        CamDevice[hCam1].SetParam((float)decimal.ToDouble(FormProdctPar.UpDown_Cam1_ExposureTime.Value));


                        CamDevice[hCam1].SetTriggerMode(7); //软触发
                        CamDevice[hCam1].SoftTriggerExec();

                        ho_SrcImg[1] = CamDevice[hCam1].TrigImage.Clone();

                        hWin1.DisPlay(ho_SrcImg[1].Clone(), null, string.Empty);

                        InsertRunMsg("1#相机已成功获取图片.", false);
                        mainDlgSendToProductDlg("mainDlgGrabDoneCam1", "");
                        break;

                    case "updataProductList":
                        UpdataProductList();
                        break;


                    case "iniRoiParam":
                        //iniRoiParam();
                        break;


                    case "TestCam0":

                        FormProdctPar.checkBox_show_result_image_cam0.Checked = true;

                        Cam0Detect(ho_SrcImg[3].Clone(), 1, out HTuple hv_CodeDateString, out HObject outLoadResultImage, out double Xoff, out double Yoff, out double Uoff);
                        FormProdctPar.checkBox_show_result_image_cam0.Checked = false;
                        FormProductParameter.Instance.hWin0.DisPlay(outLoadResultImage.Clone(), null, string.Empty);



                        break;
                    case "TestCam1":
                        StartTimeAOI = DateTime.Now;
                        FormProdctPar.checkBox_show_result_image_cam1.Checked = true;
                        Cam1Detect(ho_SrcImg[1].Clone(), 1, Convert.ToInt32(FormProdctPar.cmb_MultiLine.Text), Convert.ToDouble(FormProdctPar.numericUpDown_MultiAngle.Value),
                             Convert.ToDouble(FormProdctPar.NDU_AAoff.Value), Convert.ToDouble(FormProdctPar.NDU_BBoff.Value),
                             Convert.ToDouble(FormProdctPar.NDU_CCoff.Value), Convert.ToDouble(FormProdctPar.NDU_DDoff.Value),
                            out HTuple CodeDateString, out HObject resultImage, out HTuple Lengthes);
                        FormProdctPar.checkBox_show_result_image_cam1.Checked = false;

                        SpanTimeAOI = DateTime.Now - StartTimeAOI;
                        ElspseTimeAOI = SpanTimeAOI.TotalMilliseconds;
                        lab_AOITime1.Text = ElspseTimeAOI.ToString() + "ms";
                        FormProductParameter.Instance.hWin1.DisPlay(resultImage.Clone(), null, string.Empty);
                        break;
                    case "SaveCam1Model":
                        SaveCam1Model();
                        break;

                    case "ChangeTrigger":
                        if ((bool)data == true)
                        {
                            if (g_bCam0Open)
                            {
                                CamDevice[0].SetTriggerMode(HardTrigger); //触发方式 0、硬触发   7、软触发
                            }
                            if (g_bCam1Open)
                            {
                                CamDevice[1].SetTriggerMode(HardTrigger); //触发方式 0、硬触发   7、软触发
                            }

                        }
                        else
                        {
                            if (g_bCam0Open)
                            {
                                CamDevice[0].SetTriggerMode(SoftTrigger); //触发方式 0、硬触发   7、软触发
                            }
                            if (g_bCam1Open)
                            {
                                CamDevice[1].SetTriggerMode(SoftTrigger); //触发方式 0、硬触发   7、软触发
                            }

                        }
                        break;

                    case "TriggerTest":
                        if ((bool)data == true)
                        {
                            if (g_bCam0Open)
                            {
                                GrabImage(Cam0, HardTrigger);
                                hWin0.DisPlay(ho_SrcImg[0].Clone(), null, string.Empty);
                            }
                            if (g_bCam1Open)
                            {
                                GrabImage(Cam1, HardTrigger);
                                hWin1.DisPlay(ho_SrcImg[1].Clone(), null, string.Empty);
                            }

                        }
                        else
                        {
                            if (g_bCam0Open)
                            {
                                GrabImage(Cam0, SoftTrigger);
                                hWin0.DisPlay(ho_SrcImg[0].Clone(), null, string.Empty);
                            }
                            if (g_bCam1Open)
                            {
                                GrabImage(Cam1, SoftTrigger);
                                hWin1.DisPlay(ho_SrcImg[1].Clone(), null, string.Empty);
                            }
                        }
                        break;


                    case "setProductName":
                        comboBox_select_product.Text = (string)data;
                        break;
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ProcessRobotDlgMsg(string Message, object data)
        {
            try
            {
                switch (Message)
                {
                    case "ConnectPlc1":
                        ConnectPlc1();
                        break;


                    case "ConnectPlc2":
                        ConnectPlc2();
                        break;

                    case "ConnectPlc3":
                        ConnectPlc3();
                        break;


                    case "Plc1_send":
                        Plc1_send();
                        break;

                    case "Plc2_send":
                        Plc2_send();
                        break;

                    case "Calib9Pts":
                        Cam0Detect(ho_SrcImg[3].Clone(), 1, out HTuple hv_CodeDateString, out HObject outLoadResultImage, out double Xoff, out double Yoff, out double Uoff);
                        break;
                }
            }
            catch (HalconException CvEx)
            {
                MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region Communication  
        public void ConnectPlc1()
        {
            if (FormRbt.button_Plc1_connect.Text == "连接")
            {

                InsertRunMsg("正在连接PLC_1-IP：" + FormRbt.textBox_Plc1_ip.Text + "端口:" + FormRbt.textBox_Plc1_port.Text, false);
                PLC1 = new McProtocolTcp(FormRbt.textBox_Plc1_ip.Text, int.Parse(FormRbt.textBox_Plc1_port.Text));
                try
                {
                    if (0 == PLC1.Open())
                    {
                        InsertRunMsg("客户端PLC_1已连接.", false);
                        FormRbt.button_Plc1_connect.Text = "断开连接";
                        FormRbt.label_Plc1_connect_status.Text = "状态：已连接";
                        pictureBox_Plc1_status.BackColor = Color.Lime;
                        mainDlgSendToRobotDlg("SavePlc1ConnectPar", "");

                        //thScanPlc1Data = new Thread(ListenPlc1DValue);
                        //thScanPlc1Data.Start();
                    }
                }
                catch (HalconException CvEx)
                {
                    MessageBox.Show(CvEx.Message);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                    InsertRunMsg(ex.Message, false);
                    PLC1 = null;
                }
            }
        }

        public void ConnectPlc2()
        {
            if (FormRbt.button_Plc2_connect.Text == "连接")
            {
                InsertRunMsg("正在连接PLC_2-IP：" + FormRbt.textBox_Plc2_ip.Text + "端口:" + FormRbt.textBox_Plc2_port.Text, false);
                PLC2 = new McProtocolTcp(FormRbt.textBox_Plc2_ip.Text, int.Parse(FormRbt.textBox_Plc2_port.Text));
                try
                {
                    if (0 == PLC2.Open())
                    {
                        InsertRunMsg("客户端PLC_2已连接.", false);
                        FormRbt.button_Plc2_connect.Text = "断开连接";
                        FormRbt.label_Plc2_connect_status.Text = "状态：已连接";
                        pictureBox_Plc2_status.BackColor = Color.Lime;
                        mainDlgSendToRobotDlg("SavePlc2ConnectPar", "");
                        //thRecvPlc2Data = new Thread(GetPlc2DValue);
                        //thRecvPlc2Data.Start();
                    }
                }
                catch (HalconException CvEx)
                {
                    MessageBox.Show(CvEx.Message);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                    InsertRunMsg(ex.Message, false);
                    PLC2 = null;
                }
            }
        }

        public void ConnectPlc3()
        {
            if (FormRbt.button_Plc3_connect.Text == "连接")
            {
                InsertRunMsg("正在连接PLC_3-IP：" + FormRbt.textBox_Plc3_ip.Text + "端口:" + FormRbt.textBox_Plc3_port.Text, false);
                PLC3 = new McProtocolTcp(FormRbt.textBox_Plc3_ip.Text, int.Parse(FormRbt.textBox_Plc3_port.Text));
                try
                {
                    if (0 == PLC3.Open())
                    {
                        InsertRunMsg("客户端PLC_3已连接.", false);
                        FormRbt.button_Plc3_connect.Text = "断开连接";
                        FormRbt.label_Plc3_connect_status.Text = "状态：已连接";
                        pictureBox_Plc3_status.BackColor = Color.Lime;
                        mainDlgSendToRobotDlg("SavePlc3ConnectPar", "");
                        // thRecvPlc3Data = new Thread(GetPlc3DValue);
                        // thRecvPlc3Data.Start();
                    }
                }
                catch (HalconException CvEx)
                {
                    MessageBox.Show(CvEx.Message);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                    InsertRunMsg(ex.Message, false);
                    PLC3 = null;
                }
            }
        }

        private static void Plc1_send()
        {

            ComboBox cb = FormRobot.Instance.comboBox1;
            string buff = cb.Text;
            if (0 < buff.IndexOf(','))
            {   // “D 10、2”的模式
                string[] s = buff.Split(',');
                if (1 < s.Length)
                {
                    PlcDeviceType type;
                    int addr;
                    McProtocolApp.GetDeviceCode(s[0], out type, out addr);

                    var val = new int[int.Parse(s[1])];
                    int rtCode = McProtocolApp.IsBitDevice(type) ? FormMain.PLC1.GetBitDevice(s[0], val.Length, val) :
                                                                   FormMain.PLC1.ReadDeviceBlock(s[0], val.Length, val);
                    if (0 < rtCode)
                    {
                        FormRobot.Instance.listBox_Plc1_recv.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
                    }
                    else
                    {
                        for (int i = 0; i < val.Length; ++i)
                        {
                            FormRobot.Instance.listBox_Plc1_recv.Items.Add(type.ToString() + (addr + i).ToString(CultureInfo.InvariantCulture) + "=" + val[i].ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
            }
            else if (0 < buff.IndexOf('='))
            {
                string[] s = buff.Split('=');
                if (0 < s[0].IndexOf("..", System.StringComparison.Ordinal))
                {   // “D 10..12=0”的模式
                    string[] t = s[0].Replace("..", "=").Split('=');
                    int m;
                    int n = int.Parse(t[1]);
                    PlcDeviceType type;
                    McProtocolApp.GetDeviceCode(t[0], out type, out m);
                    var data = new int[n - m + 1];
                    int v = int.Parse(s[1]);
                    for (int i = 0; i < data.Length; ++i)
                    {
                        data[i] = v;
                    }
                    int rtCode = McProtocolApp.IsBitDevice(type) ? FormMain.PLC1.SetBitDevice(t[0], data.Length, data) :
                                                                    FormMain.PLC1.WriteDeviceBlock(t[0], data.Length, data);
                    FormRobot.Instance.listBox_Plc1_recv.Items.Add(buff.ToUpper());
                    if (0 < rtCode)
                    {
                        FormRobot.Instance.listBox_Plc1_recv.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
                    }
                }
                else
                {   // “D 10=0”的模式
                    PlcDeviceType type;
                    int addr;
                    McProtocolApp.GetDeviceCode(s[0], out type, out addr);

                    int val = int.Parse(s[1]) | 0x0002 | 0x0004 | 0x0008 | 0x0010;
                    int rtCode;
                    if (McProtocolApp.IsBitDevice(type))
                    {
                        var data = new int[1];
                        data[0] = val;
                        rtCode = FormMain.PLC1.SetBitDevice(s[0], data.Length, data);
                    }
                    else
                    {
                        rtCode = FormMain.PLC1.SetDevice(s[0], val);
                    }
                    FormRobot.Instance.listBox_Plc1_recv.Items.Add(buff.ToUpper());
                    if (0 < rtCode)
                    {
                        FormRobot.Instance.listBox_Plc1_recv.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
                    }
                }
            }
            else
            {   // “D 10”的模式
                PlcDeviceType type;
                int addr;
                McProtocolApp.GetDeviceCode(buff.ToUpper(), out type, out addr);

                int n;
                int rtCode;
                if (McProtocolApp.IsBitDevice(type))
                {
                    var data = new int[1];
                    rtCode = FormMain.PLC1.GetBitDevice
                        (buff, data.Length, data);
                    n = data[0];
                }
                else
                {
                    rtCode = FormMain.PLC1.GetDevice(buff.ToUpper(), out n);
                }
                FormRobot.Instance.listBox_Plc1_recv.Items.Add(buff.ToUpper() + "=" + n.ToString(CultureInfo.InvariantCulture));
                if (0 < rtCode)
                {
                    FormRobot.Instance.listBox_Plc1_recv.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
                }
            }
            FormRobot.Instance.listBox_Plc1_recv.SelectedIndex = FormRobot.Instance.listBox_Plc1_recv.Items.Count - 1;
            cb.Items.Insert(0, cb.Text);
            cb.Text = "";
        }

        private static void Plc2_send()
        {

            ComboBox cb = FormRobot.Instance.comboBox2;
            string buff = cb.Text;
            if (0 < buff.IndexOf(','))
            {   // “D 10、2”的模式
                string[] s = buff.Split(',');
                if (1 < s.Length)
                {
                    PlcDeviceType type;
                    int addr;
                    McProtocolApp.GetDeviceCode(s[0], out type, out addr);

                    var val = new int[int.Parse(s[1])];
                    int rtCode = McProtocolApp.IsBitDevice(type) ? FormMain.PLC2.GetBitDevice(s[0], val.Length, val) :
                                                                   FormMain.PLC2.ReadDeviceBlock(s[0], val.Length, val);
                    if (0 < rtCode)
                    {
                        FormRobot.Instance.listBox_Plc2_recv.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
                    }
                    else
                    {
                        for (int i = 0; i < val.Length; ++i)
                        {
                            FormRobot.Instance.listBox_Plc2_recv.Items.Add(type.ToString() + (addr + i).ToString(CultureInfo.InvariantCulture) + "=" + val[i].ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
            }
            else if (0 < buff.IndexOf('='))
            {
                string[] s = buff.Split('=');
                if (0 < s[0].IndexOf("..", System.StringComparison.Ordinal))
                {   // “D 10..12=0”的模式
                    string[] t = s[0].Replace("..", "=").Split('=');
                    int m;
                    int n = int.Parse(t[1]);
                    PlcDeviceType type;
                    McProtocolApp.GetDeviceCode(t[0], out type, out m);
                    var data = new int[n - m + 1];
                    int v = int.Parse(s[1]);
                    for (int i = 0; i < data.Length; ++i)
                    {
                        data[i] = v;
                    }
                    int rtCode = McProtocolApp.IsBitDevice(type) ? FormMain.PLC2.SetBitDevice(t[0], data.Length, data) :
                                                                    FormMain.PLC2.WriteDeviceBlock(t[0], data.Length, data);
                    FormRobot.Instance.listBox_Plc2_recv.Items.Add(buff.ToUpper());
                    if (0 < rtCode)
                    {
                        FormRobot.Instance.listBox_Plc2_recv.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
                    }
                }
                else
                {   // “D 10=0”的模式
                    PlcDeviceType type;
                    int addr;
                    McProtocolApp.GetDeviceCode(s[0], out type, out addr);

                    int val = int.Parse(s[1]) | 0x0002 | 0x0004 | 0x0008 | 0x0010;
                    int rtCode;
                    if (McProtocolApp.IsBitDevice(type))
                    {
                        var data = new int[1];
                        data[0] = val;
                        rtCode = FormMain.PLC2.SetBitDevice(s[0], data.Length, data);
                    }
                    else
                    {
                        rtCode = FormMain.PLC2.SetDevice(s[0], val);
                    }
                    FormRobot.Instance.listBox_Plc2_recv.Items.Add(buff.ToUpper());
                    if (0 < rtCode)
                    {
                        FormRobot.Instance.listBox_Plc2_recv.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
                    }
                }
            }
            else
            {   // “D 10”的模式
                PlcDeviceType type;
                int addr;
                McProtocolApp.GetDeviceCode(buff.ToUpper(), out type, out addr);

                int n;
                int rtCode;
                if (McProtocolApp.IsBitDevice(type))
                {
                    var data = new int[1];
                    rtCode = FormMain.PLC2.GetBitDevice
                        (buff, data.Length, data);
                    n = data[0];
                }
                else
                {
                    rtCode = FormMain.PLC2.GetDevice(buff.ToUpper(), out n);
                }
                FormRobot.Instance.listBox_Plc2_recv.Items.Add(buff.ToUpper() + "=" + n.ToString(CultureInfo.InvariantCulture));
                if (0 < rtCode)
                {
                    FormRobot.Instance.listBox_Plc2_recv.Items.Add("ERROR:0x" + rtCode.ToString("X4"));
                }
            }
            FormRobot.Instance.listBox_Plc2_recv.SelectedIndex = FormRobot.Instance.listBox_Plc2_recv.Items.Count - 1;
            cb.Items.Insert(0, cb.Text);
            cb.Text = "";
        }

        public void ListenPlc1DValue(object obj)
        {
            while (true)
            {
                try
                {
                    if (pictureBox_Plc1_status.BackColor == Color.Lime && PLC1 != null)
                    {
                        GetMcPlc("D 2100", PLC1, out List<string> McData2100);


                    }
                }
                catch (HalconException CvEx)
                {
                    MessageBox.Show(CvEx.Message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        #endregion

        public void Cam0Detect(HObject SrcImage, int StationIndex, out HTuple hv_DecodedDataStrings, out HObject ho_ResultImage, out double Xoffset, out double Yoffset, out double Angleoffset)
        {
            Xoffset = 999999;
            Yoffset = 999999;
            Angleoffset = 999999;
            hv_DecodedDataStrings = null;
            ho_ResultImage = SrcImage;

            HObject ho_ROIConcat;
            HOperatorSet.GenEmptyObj(out ho_ROIConcat);
            HOperatorSet.GenEmptyObj(out ho_ResultImage);

            try
            {
                string strProductName = comboBox_select_product.SelectedItem.ToString();
                if (!File.Exists("product/" + strProductName + "/cam0par.ini"))
                {
                    MessageBox.Show("product/" + strProductName + "/cam0par.ini", "文件缺失");
                    return;
                }

                string IniPath = "product/" + strProductName + "/cam0par.ini";
                InIClass Inicam0par = new InIClass(IniPath);

                int num = Convert.ToInt32(Inicam0par.Read("product", "roi_num"));
                Roi[] LoadRoi = new Roi[num];
                for (int i = 1; i <= num; i++)
                {
                    string iniSection = "Load-" + i.ToString();
                    LoadRoi[i - 1].Row1 = Convert.ToInt32(Inicam0par.Read(iniSection, "y1"));
                    LoadRoi[i - 1].Column1 = Convert.ToInt32(Inicam0par.Read(iniSection, "x1"));
                    LoadRoi[i - 1].Row2 = Convert.ToInt32(Inicam0par.Read(iniSection, "y2"));
                    LoadRoi[i - 1].Column2 = Convert.ToInt32(Inicam0par.Read(iniSection, "x2"));
                }
                //读取
                HTuple hv_ProductName = strProductName;
                // ReadCapParams(out HObject ho_ModelImage, hv_ProductName, out HTuple hv_CalibDataTest, out HTuple hv_CodeHandle);
                genRectRoi(LoadRoi[0].Row1, LoadRoi[0].Column1, LoadRoi[0].Row2, LoadRoi[0].Column2, out ho_LoadRoiCode);
                genRectRoi(LoadRoi[1].Row1, LoadRoi[1].Column1, LoadRoi[1].Row2, LoadRoi[1].Column2, out ho_LoadRoiMark);


                ho_ROIConcat.Dispose();
                HOperatorSet.ConcatObj(ho_LoadRoiCode, ho_LoadRoiMark, out ho_ROIConcat);

                ho_ResultImage.Dispose();

                HTuple GrayMin = (double)FormProdctPar.numericUD_Cam0GrayMin.Value;

                LocationCap(SrcImage, ho_ROIConcat, out ho_ResultImage, DataCodeHandle, 0, FormProdctPar.CombLocMethod.Text, GrayMin, out hv_DecodedDataStrings,
                    out HTuple hv_RowCentre, out HTuple hv_ColumnCentre, out HTuple hv_Radius, out HTuple hv_CodeRow,
                    out HTuple hv_CodeColumn, out HTuple hv_Row, out HTuple hv_Col, out HTuple hv_Angle);

                if (FormProdctPar.checkBox_save_origion_image_cam0.Checked)
                {
                    saveImage(hv_DecodedDataStrings, SrcImage.Clone(), 0, 0, StationIndex);
                }
                if (FormProdctPar.checkBox_save_ok_image_cam0.Checked)
                {
                    saveImage(hv_DecodedDataStrings, ho_ResultImage.Clone(), 0, 1, StationIndex);
                }

                //计算偏移
                HTuple Std_Aangle = null, Std_AY = null, Std_AX = null, RotateC = null, RotateR = null;
                if (hv_Angle != null)
                {
                    switch (StationIndex)
                    {
                        case 1:
                            hv_CalibData = hv_CalibData1;
                            Std_Aangle = Convert.ToDouble(FormProdctPar.Std_Aangle1.Value) / 180 * 3.1415926;
                            Std_AY = Convert.ToDouble(FormProdctPar.Std_AY1.Value);
                            Std_AX = Convert.ToDouble(FormProdctPar.Std_AX1.Value);
                            RotateC = Convert.ToDouble(FormProdctPar.numUD_1RotateC.Value);
                            RotateR = Convert.ToDouble(FormProdctPar.numUD_1RotateR.Value);
                            break;
                        case 2:
                            hv_CalibData = hv_CalibData2;
                            Std_Aangle = Convert.ToDouble(FormProdctPar.Std_Aangle2.Value) / 180 * 3.1415926;
                            Std_AY = Convert.ToDouble(FormProdctPar.Std_AY2.Value);
                            Std_AX = Convert.ToDouble(FormProdctPar.Std_AX2.Value);
                            RotateC = Convert.ToDouble(FormProdctPar.numUD_2RotateC.Value);
                            RotateR = Convert.ToDouble(FormProdctPar.numUD_2RotateR.Value);

                            break;
                        case 3:
                            hv_CalibData = hv_CalibData3;
                            Std_Aangle = Convert.ToDouble(FormProdctPar.Std_Aangle3.Value) / 180 * 3.1415926;
                            Std_AY = Convert.ToDouble(FormProdctPar.Std_AY3.Value);
                            Std_AX = Convert.ToDouble(FormProdctPar.Std_AX3.Value);
                            RotateC = Convert.ToDouble(FormProdctPar.numUD_3RotateC.Value);
                            RotateR = Convert.ToDouble(FormProdctPar.numUD_3RotateR.Value);
                            break;
                        case 4:
                            hv_CalibData = hv_CalibData4;
                            Std_Aangle = Convert.ToDouble(FormProdctPar.Std_Bangle1.Value) / 180 * 3.1415926;
                            Std_AY = Convert.ToDouble(FormProdctPar.Std_BY1.Value);
                            Std_AX = Convert.ToDouble(FormProdctPar.Std_BX1.Value);
                            RotateC = Convert.ToDouble(FormProdctPar.numUD_4RotateC.Value);
                            RotateR = Convert.ToDouble(FormProdctPar.numUD_4RotateR.Value);
                            break;
                        case 5:
                            hv_CalibData = hv_CalibData5;
                            Std_Aangle = Convert.ToDouble(FormProdctPar.Std_Bangle2.Value) / 180 * 3.1415926;
                            Std_AY = Convert.ToDouble(FormProdctPar.Std_BY2.Value);
                            Std_AX = Convert.ToDouble(FormProdctPar.Std_BX2.Value);
                            RotateC = Convert.ToDouble(FormProdctPar.numUD_5RotateC.Value);
                            RotateR = Convert.ToDouble(FormProdctPar.numUD_5RotateR.Value);

                            break;
                        case 6:
                            hv_CalibData = hv_CalibData6;
                            Std_Aangle = Convert.ToDouble(FormProdctPar.Std_Bangle3.Value) / 180 * 3.1415926;
                            Std_AY = Convert.ToDouble(FormProdctPar.Std_BY3.Value);
                            Std_AX = Convert.ToDouble(FormProdctPar.Std_BX3.Value);
                            RotateC = Convert.ToDouble(FormProdctPar.numUD_6RotateC.Value);
                            RotateR = Convert.ToDouble(FormProdctPar.numUD_6RotateR.Value);
                            break;
                        default:
                            break;
                    }
                    //吸嘴 X   对应 图像 Row
                    PixCoordToRobotCoord(hv_CalibData, RotateR, RotateC,
                                     hv_Angle, hv_Row, hv_Col,
                                   Std_Aangle, Std_AX, Std_AY,
                                  out HTuple hv_OffSetX,
                                  out HTuple hv_OffSetY,
                                  out HTuple hv_OffSetAngle
                                );

                    // mainDlgSendToProductDlg("DispCam0Rst", ho_ResultImage.Clone());
                    string strhvRow = hv_Row.D.ToString("#0.00");//点后面几个0就保留几位 
                    string strhvCol = hv_Col.D.ToString("#0.00");
                    string strhvAngle = hv_Angle.D.ToString("#0.00");
                    string strhvOffSetX = hv_OffSetX.D.ToString("#0.00");//点后面几个0就保留几位 
                    string strhvOffSetY = hv_OffSetY.D.ToString("#0.00");
                    string strhvOffSetAngle = hv_OffSetAngle.D.ToString("#0.00");

                    InsertRunMsg(StationIndex + "位置： nowRow:" + strhvRow + " nowCol:" + strhvCol + " 角度:" + strhvAngle, false);
                    // InsertRunMsg(StationIndex + "Std：  row:" + Std_AX + " col:" + Std_AY + " Angle:" + Std_Aangle, false);

                    Xoffset = hv_OffSetX.D;
                    Yoffset = hv_OffSetY.D;
                    Angleoffset = hv_OffSetAngle.D;
                    Angleoffset = Angleoffset / 3.1415926 * 180;  //弧度转角度

                    hWin0.DisPlay(ho_ResultImage.Clone(), null, Angleoffset.ToString("#0.0"));

                    InsertRunMsg(StationIndex + "偏移量： X:" + strhvOffSetX + " Y:" + strhvOffSetY + " 角度:" + (Angleoffset * 5.5555).ToString(), false);
                }
            }
            catch (HalconException CvEx)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(CvEx.StackTrace, "08") + CvEx.Message);
                //MessageBox.Show(CvEx.Message);
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "08") + ex.Message);

            }

        }

        public void Cam1Detect(HObject SrcImage, int StationIndex, int GRRmultiLine, double GRRmultiAngle, double AAOff, double BBOff, double CCOff, double DDOff, out HTuple hv_deCodeDataString, out HObject resultImage,
            out HTuple hv_Lengthes)
        {

            hv_deCodeDataString = "000000";
            hv_Lengthes = null;
            HWin hWinID = hWin1;
            if (StationIndex == 1) hWinID = hWin1;
            if (StationIndex == 2) hWinID = hWin2;
            if (StationIndex == 3) hWinID = hWin3;

            resultImage = SrcImage.Clone();

            try
            {
                //AOI
                string strProductName = comboBox_select_product.SelectedItem.ToString();
                if (!File.Exists("product/" + strProductName + "/cam1par.ini"))
                {
                    MessageBox.Show("product/" + strProductName + "/cam1par.ini", "文件缺失");
                    return;
                }
                string IniPath = "product/" + strProductName + "/cam1par.ini";
                InIClass Inicam1par = new InIClass(IniPath);

                int num = Convert.ToInt32(Inicam1par.Read("product", "roi_num"));
                Roi[] AOIRoi = new Roi[num];
                for (int i = 1; i <= num; i++)
                {
                    string iniSection = "Mark" + i.ToString();
                    AOIRoi[i - 1].Row1 = Convert.ToInt32(Inicam1par.Read(iniSection, "y1"));
                    AOIRoi[i - 1].Column1 = Convert.ToInt32(Inicam1par.Read(iniSection, "x1"));
                    AOIRoi[i - 1].Row2 = Convert.ToInt32(Inicam1par.Read(iniSection, "y2"));
                    AOIRoi[i - 1].Column2 = Convert.ToInt32(Inicam1par.Read(iniSection, "x2"));
                }
                //读取
                HTuple hv_ProductName = strProductName;
                genRectRoi(AOIRoi[0].Row1, AOIRoi[0].Column1, AOIRoi[0].Row2, AOIRoi[0].Column2, out HObject ho_AOIRoiCode);
                genRectRoi(AOIRoi[1].Row1, AOIRoi[1].Column1, AOIRoi[1].Row2, AOIRoi[1].Column2, out HObject ho_AOIRoiMark);
                resultImage.Dispose();

                MeasureCap(SrcImage, ho_AOIRoiCode, ho_AOIRoiMark, GRRmultiLine, GRRmultiAngle, out resultImage, FormMain.DataCodeHandle, FormMain.hv_CoordAngleOffset, FormMain.modelModeCodeRow,
                    FormMain.ModeCodeCol, FormMain.ModeCodeAngle, out hv_deCodeDataString, out HTuple hv_RowCentre, out HTuple hv_ColCentre, out HTuple hv_Radius, out hv_Lengthes);

                //补偿
                hv_Lengthes[0] = hv_Lengthes[0] + AAOff;
                hv_Lengthes[1] = hv_Lengthes[1] + BBOff;
                hv_Lengthes[2] = hv_Lengthes[2] + CCOff;
                hv_Lengthes[3] = hv_Lengthes[3] + DDOff;

                InsertRunMsg("AOI--" + " Result: " + hv_Lengthes[0] + "/  " + hv_Lengthes[1] + "/  " + hv_Lengthes[2] + "/  " + hv_Lengthes[3], false);

            }
            catch (HalconException CvEx)
            {
                //MessageBox.Show(CvEx.Message);
                LogWriter.WriteLog(LogWriter.ErrCode(CvEx.StackTrace, "08") + CvEx.Message);

                hv_deCodeDataString = "000000";
                hv_Lengthes = null;

            }
            catch (Exception ex)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "08") + ex.Message);

                hv_deCodeDataString = "000000";
                hv_Lengthes = null;


            }
        }

        public static void genRectRoi(double Row1, double Col1, double Row2, double Col2, out HObject RectRoi)
        {
            HOperatorSet.GenEmptyObj(out RectRoi);
            try
            {
                HOperatorSet.GenRectangle1(out RectRoi, Row1, Col1, Row2, Col2);
            }
            catch (HalconException CvEx)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(CvEx.StackTrace, "08") + CvEx.Message);
            }
            catch (Exception ex)
            {
                LogWriter.WriteLog(LogWriter.ErrCode(ex.StackTrace, "08") + ex.Message);
            }
        }

        public void gen_arrow_contour_xld(out HObject ho_Arrow, HTuple hv_Row1, HTuple hv_Column1, HTuple hv_Row2, HTuple hv_Column2, HTuple hv_HeadLength, HTuple hv_HeadWidth)
        {



            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_TempArrow = null;

            // Local control variables 

            HTuple hv_Length = null, hv_ZeroLengthIndices = null;
            HTuple hv_DR = null, hv_DC = null, hv_HalfHeadWidth = null;
            HTuple hv_RowP1 = null, hv_ColP1 = null, hv_RowP2 = null;
            HTuple hv_ColP2 = null, hv_Index = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Arrow);
            HOperatorSet.GenEmptyObj(out ho_TempArrow);
            //This procedure generates arrow shaped XLD contours,
            //pointing from (Row1, Column1) to (Row2, Column2).
            //If starting and end point are identical, a contour consisting
            //of a single point is returned.
            //
            //input parameteres:
            //Row1, Column1: Coordinates of the arrows' starting points
            //Row2, Column2: Coordinates of the arrows' end points
            //HeadLength, HeadWidth: Size of the arrow heads in pixels
            //
            //output parameter:
            //Arrow: The resulting XLD contour
            //
            //The input tuples Row1, Column1, Row2, and Column2 have to be of
            //the same length.
            //HeadLength and HeadWidth either have to be of the same length as
            //Row1, Column1, Row2, and Column2 or have to be a single element.
            //If one of the above restrictions is violated, an error will occur.
            //
            //
            //Init
            ho_Arrow.Dispose();
            HOperatorSet.GenEmptyObj(out ho_Arrow);
            //
            //Calculate the arrow length
            HOperatorSet.DistancePp(hv_Row1, hv_Column1, hv_Row2, hv_Column2, out hv_Length);
            //
            //Mark arrows with identical start and end point
            //(set Length to -1 to avoid division-by-zero exception)
            hv_ZeroLengthIndices = hv_Length.TupleFind(0);
            if ((int)(new HTuple(hv_ZeroLengthIndices.TupleNotEqual(-1))) != 0)
            {
                if (hv_Length == null)
                    hv_Length = new HTuple();
                hv_Length[hv_ZeroLengthIndices] = -1;
            }
            //
            //Calculate auxiliary variables.
            hv_DR = (1.0 * (hv_Row2 - hv_Row1)) / hv_Length;
            hv_DC = (1.0 * (hv_Column2 - hv_Column1)) / hv_Length;
            hv_HalfHeadWidth = hv_HeadWidth / 2.0;
            //
            //Calculate end points of the arrow head.
            hv_RowP1 = (hv_Row1 + ((hv_Length - hv_HeadLength) * hv_DR)) + (hv_HalfHeadWidth * hv_DC);
            hv_ColP1 = (hv_Column1 + ((hv_Length - hv_HeadLength) * hv_DC)) - (hv_HalfHeadWidth * hv_DR);
            hv_RowP2 = (hv_Row1 + ((hv_Length - hv_HeadLength) * hv_DR)) - (hv_HalfHeadWidth * hv_DC);
            hv_ColP2 = (hv_Column1 + ((hv_Length - hv_HeadLength) * hv_DC)) + (hv_HalfHeadWidth * hv_DR);
            //
            //Finally create output XLD contour for each input point pair
            for (hv_Index = 0; (int)hv_Index <= (int)((new HTuple(hv_Length.TupleLength())) - 1); hv_Index = (int)hv_Index + 1)
            {
                if ((int)(new HTuple(((hv_Length.TupleSelect(hv_Index))).TupleEqual(-1))) != 0)
                {
                    //Create_ single points for arrows with identical start and end point
                    ho_TempArrow.Dispose();
                    HOperatorSet.GenContourPolygonXld(out ho_TempArrow, hv_Row1.TupleSelect(hv_Index),
                        hv_Column1.TupleSelect(hv_Index));
                }
                else
                {
                    //Create arrow contour
                    ho_TempArrow.Dispose();
                    HOperatorSet.GenContourPolygonXld(out ho_TempArrow, ((((((((((hv_Row1.TupleSelect(
                        hv_Index))).TupleConcat(hv_Row2.TupleSelect(hv_Index)))).TupleConcat(
                        hv_RowP1.TupleSelect(hv_Index)))).TupleConcat(hv_Row2.TupleSelect(hv_Index)))).TupleConcat(
                        hv_RowP2.TupleSelect(hv_Index)))).TupleConcat(hv_Row2.TupleSelect(hv_Index)),
                        ((((((((((hv_Column1.TupleSelect(hv_Index))).TupleConcat(hv_Column2.TupleSelect(
                        hv_Index)))).TupleConcat(hv_ColP1.TupleSelect(hv_Index)))).TupleConcat(
                        hv_Column2.TupleSelect(hv_Index)))).TupleConcat(hv_ColP2.TupleSelect(
                        hv_Index)))).TupleConcat(hv_Column2.TupleSelect(hv_Index)));
                }
                {
                    HObject ExpTmpOutVar_0;
                    HOperatorSet.ConcatObj(ho_Arrow, ho_TempArrow, out ExpTmpOutVar_0);
                    ho_Arrow.Dispose();
                    ho_Arrow = ExpTmpOutVar_0;
                }
            }
            ho_TempArrow.Dispose();

            return;
        }



        #region 标定
        public void PixCoordToRobotCoord(HTuple hv_HomMat2DCalib9Pts, HTuple hv_RotateCentreRow,
        HTuple hv_RotateCentreCol, HTuple hv_angle, HTuple hv_X, HTuple hv_Y, HTuple hv_std_angle,
         HTuple hv_stdrow, HTuple hv_stdcol, out HTuple hv_offsetX, out HTuple hv_offsetY,
         out HTuple hv_offsetAngle)
        {



            // Local iconic variables 

            // Local control variables 

            HTuple hv_HomMat2D = new HTuple(), hv_Qx = new HTuple();
            HTuple hv_Qy = new HTuple(), hv_Qx1 = new HTuple(), hv_Qy1 = new HTuple();
            HTuple hv_Qx2 = new HTuple(), hv_Qy2 = new HTuple(), hv_Exception = null;
            // Initialize local and output iconic variables 
            hv_offsetX = new HTuple();
            hv_offsetY = new HTuple();
            hv_offsetAngle = new HTuple();

            try
            {
                HOperatorSet.VectorAngleToRigid(hv_RotateCentreRow, hv_RotateCentreCol, hv_angle,
                    hv_RotateCentreRow, hv_RotateCentreCol, hv_std_angle, out hv_HomMat2D);

                HOperatorSet.AffineTransPoint2d(hv_HomMat2D, hv_X, hv_Y, out hv_Qx, out hv_Qy);

                HOperatorSet.AffineTransPoint2d(hv_HomMat2DCalib9Pts, hv_Qx, hv_Qy, out hv_Qx1, out hv_Qy1);

                HOperatorSet.AffineTransPoint2d(hv_HomMat2DCalib9Pts, hv_stdrow, hv_stdcol, out hv_Qx2, out hv_Qy2);

                hv_offsetX = hv_Qx2 - hv_Qx1;
                hv_offsetY = hv_Qy2 - hv_Qy1;
                hv_offsetAngle = hv_std_angle - hv_angle;
            }
            // catch (Exception) 
            catch (HalconException HDevExpDefaultException1)
            {
                HDevExpDefaultException1.ToHTuple(out hv_Exception);
                logFile("PixCoordToRobotCoord fail", hv_Exception);
            }

            return;
        }
        #endregion

        #region 测量算法
        public void Find_Mark_Region(HObject ho_SrcImage, HObject ho_Roi, out HObject ho_MarkRegion)
        {



            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_ImageReduced1 = null, ho_ImageMean = null;
            HObject ho_ImageScaled = null, ho_RegionOpening1 = null, ho_SelectedRegions = null;
            HObject ho_RegionClosing = null, ho_RegionOpening = null, ho_RegionTrans = null;
            HObject ho_RegionDifference = null, ho_ConnectedRegions1 = null;

            // Local control variables 

            HTuple hv_Area = new HTuple(), hv_Row = new HTuple();
            HTuple hv_Column = new HTuple(), hv_Min = new HTuple();
            HTuple hv_Max = new HTuple(), hv_Range = new HTuple();
            HTuple hv_Mult = new HTuple(), hv_Add = new HTuple(), hv_UsedThreshold = new HTuple();
            HTuple hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_MarkRegion);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced1);
            HOperatorSet.GenEmptyObj(out ho_ImageMean);
            HOperatorSet.GenEmptyObj(out ho_ImageScaled);
            HOperatorSet.GenEmptyObj(out ho_RegionOpening1);
            HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
            HOperatorSet.GenEmptyObj(out ho_RegionClosing);
            HOperatorSet.GenEmptyObj(out ho_RegionOpening);
            HOperatorSet.GenEmptyObj(out ho_RegionTrans);
            HOperatorSet.GenEmptyObj(out ho_RegionDifference);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions1);
            try
            {
                ho_MarkRegion.Dispose();
                HOperatorSet.GenEmptyObj(out ho_MarkRegion);
                try
                {
                    ho_MarkRegion.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_MarkRegion);
                    HOperatorSet.AreaCenter(ho_Roi, out hv_Area, out hv_Row, out hv_Column);
                    ho_ImageReduced1.Dispose();
                    HOperatorSet.ReduceDomain(ho_SrcImage, ho_Roi, out ho_ImageReduced1);
                    ho_ImageMean.Dispose();
                    HOperatorSet.MeanImage(ho_ImageReduced1, out ho_ImageMean, 9, 9);

                    HOperatorSet.MinMaxGray(ho_ImageMean, ho_ImageMean, 0, out hv_Min, out hv_Max,
                        out hv_Range);

                    hv_Mult = 255 / (hv_Max - hv_Min);
                    hv_Add = (-hv_Mult) * hv_Min;
                    ho_ImageScaled.Dispose();
                    HOperatorSet.ScaleImage(ho_ImageMean, out ho_ImageScaled, hv_Mult, hv_Add);


                    //scale_image (ImageReduced1, ImageScaled, 9.80769, -1010)
                    ho_MarkRegion.Dispose();
                    HOperatorSet.BinaryThreshold(ho_ImageScaled, out ho_MarkRegion, "max_separability",
                        "dark", out hv_UsedThreshold);
                    ho_RegionOpening1.Dispose();
                    HOperatorSet.OpeningCircle(ho_MarkRegion, out ho_RegionOpening1, 2);
                    ho_MarkRegion.Dispose();
                    HOperatorSet.Connection(ho_RegionOpening1, out ho_MarkRegion);
                    ho_SelectedRegions.Dispose();
                    HOperatorSet.SelectShapeStd(ho_MarkRegion, out ho_SelectedRegions, "max_area",
                        70);
                    ho_RegionClosing.Dispose();
                    HOperatorSet.ClosingCircle(ho_SelectedRegions, out ho_RegionClosing, 5);
                    ho_RegionOpening.Dispose();
                    HOperatorSet.OpeningCircle(ho_RegionClosing, out ho_RegionOpening, 5);
                    ho_RegionTrans.Dispose();
                    HOperatorSet.ShapeTrans(ho_RegionOpening, out ho_RegionTrans, "convex");
                    ho_RegionDifference.Dispose();
                    HOperatorSet.Difference(ho_RegionTrans, ho_RegionOpening, out ho_RegionDifference
                        );
                    ho_ConnectedRegions1.Dispose();
                    HOperatorSet.Connection(ho_RegionDifference, out ho_ConnectedRegions1);
                    ho_MarkRegion.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions1, out ho_MarkRegion, "max_area",
                        70);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.OpeningCircle(ho_MarkRegion, out ExpTmpOutVar_0, 1);
                        ho_MarkRegion.Dispose();
                        ho_MarkRegion = ExpTmpOutVar_0;
                    }

                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("Find_Mark_Region fail", hv_Exception);
                }
                ho_ImageReduced1.Dispose();
                ho_ImageMean.Dispose();
                ho_ImageScaled.Dispose();
                ho_RegionOpening1.Dispose();
                ho_SelectedRegions.Dispose();
                ho_RegionClosing.Dispose();
                ho_RegionOpening.Dispose();
                ho_RegionTrans.Dispose();
                ho_RegionDifference.Dispose();
                ho_ConnectedRegions1.Dispose();

                return;

            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_ImageReduced1.Dispose();
                ho_ImageMean.Dispose();
                ho_ImageScaled.Dispose();
                ho_RegionOpening1.Dispose();
                ho_SelectedRegions.Dispose();
                ho_RegionClosing.Dispose();
                ho_RegionOpening.Dispose();
                ho_RegionTrans.Dispose();
                ho_RegionDifference.Dispose();
                ho_ConnectedRegions1.Dispose();

                throw HDevExpDefaultException;
            }
        }

        public void GetCircleAOI(HObject ho_Srcimage, out HObject ho_Circle,
            out HTuple hv_Row, out HTuple hv_Column, out HTuple hv_Radius)
        {



            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_Region1 = null, ho_ConnectedRegions1 = null;
            HObject ho_SelectedRegions = null, ho_RegionUnion = null, ho_Circle1 = null;
            HObject ho_Circle2 = null, ho_ImageReduced1 = null, ho_Region3 = null;
            HObject ho_RegionOpening = null, ho_RegionBorder = null;

            // Local control variables 

            HTuple hv_Row2 = new HTuple(), hv_Column2 = new HTuple();
            HTuple hv_Radius1 = new HTuple(), hv_Radius2 = new HTuple();
            HTuple hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Circle);
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions1);
            HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
            HOperatorSet.GenEmptyObj(out ho_RegionUnion);
            HOperatorSet.GenEmptyObj(out ho_Circle1);
            HOperatorSet.GenEmptyObj(out ho_Circle2);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced1);
            HOperatorSet.GenEmptyObj(out ho_Region3);
            HOperatorSet.GenEmptyObj(out ho_RegionOpening);
            HOperatorSet.GenEmptyObj(out ho_RegionBorder);
            try
            {

                ho_Circle.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Circle);
                hv_Row = -1;
                hv_Column = -1;
                hv_Radius = -1;
                try
                {

                    ho_Region1.Dispose();
                    HOperatorSet.Threshold(ho_Srcimage, out ho_Region1, 210, 256);
                    ho_ConnectedRegions1.Dispose();
                    HOperatorSet.Connection(ho_Region1, out ho_ConnectedRegions1);
                    ho_SelectedRegions.Dispose();
                    HOperatorSet.SelectShape(ho_ConnectedRegions1, out ho_SelectedRegions, (new HTuple("area")).TupleConcat(
                        "row"), "and", (new HTuple(83000)).TupleConcat(300), (new HTuple(9999999)).TupleConcat(
                        9999));
                    ho_RegionUnion.Dispose();
                    HOperatorSet.Union1(ho_SelectedRegions, out ho_RegionUnion);
                    HOperatorSet.SmallestCircle(ho_RegionUnion, out hv_Row2, out hv_Column2,
                        out hv_Radius1);
                    //
                    ho_Circle1.Dispose();
                    HOperatorSet.GenCircle(out ho_Circle1, hv_Row2, hv_Column2, hv_Radius1);
                    ho_Circle2.Dispose();
                    HOperatorSet.GenCircle(out ho_Circle2, hv_Row2, hv_Column2, hv_Radius1 - 280);
                    ho_ImageReduced1.Dispose();
                    HOperatorSet.ReduceDomain(ho_Srcimage, ho_Circle2, out ho_ImageReduced1);
                    ho_Region3.Dispose();
                    HOperatorSet.Threshold(ho_ImageReduced1, out ho_Region3, 0, 210);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.FillUp(ho_Region3, out ExpTmpOutVar_0);
                        ho_Region3.Dispose();
                        ho_Region3 = ExpTmpOutVar_0;
                    }
                    ho_RegionOpening.Dispose();
                    HOperatorSet.OpeningCircle(ho_Region3, out ho_RegionOpening, 5);

                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.OpeningCircle(ho_RegionOpening, out ExpTmpOutVar_0, 1900);
                        ho_RegionOpening.Dispose();
                        ho_RegionOpening = ExpTmpOutVar_0;
                    }

                    ho_RegionBorder.Dispose();
                    HOperatorSet.Boundary(ho_RegionOpening, out ho_RegionBorder, "inner_filled");
                    HOperatorSet.SmallestCircle(ho_RegionBorder, out hv_Row, out hv_Column, out hv_Radius);


                    //
                    ho_Circle.Dispose();
                    HOperatorSet.GenCircle(out ho_Circle, hv_Row, hv_Column, hv_Radius);


                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("GetCircleAOI fail", hv_Exception);
                }
                ho_Region1.Dispose();
                ho_ConnectedRegions1.Dispose();
                ho_SelectedRegions.Dispose();
                ho_RegionUnion.Dispose();
                ho_Circle1.Dispose();
                ho_Circle2.Dispose();
                ho_ImageReduced1.Dispose();
                ho_Region3.Dispose();
                ho_RegionOpening.Dispose();
                ho_RegionBorder.Dispose();

                return;


            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Region1.Dispose();
                ho_ConnectedRegions1.Dispose();
                ho_SelectedRegions.Dispose();
                ho_RegionUnion.Dispose();
                ho_Circle1.Dispose();
                ho_Circle2.Dispose();
                ho_ImageReduced1.Dispose();
                ho_Region3.Dispose();
                ho_RegionOpening.Dispose();
                ho_RegionBorder.Dispose();

                throw HDevExpDefaultException;
            }
        }

        public void MeasureCap(HObject ho_SrcImage, HObject ho_Roi_Code, HObject ho_Roi_Mark, int GRRmultiLine, double GRRmultiAngle,
            out HObject ho_ResultImage, HTuple hv_DataCodeHandle, HTuple hv_MarkAngle, HTuple hv_modelCodeRow,
            HTuple hv_modelCodeCol, HTuple hv_modelCodeAngle, out HTuple hv_DecodedDataStrings,
            out HTuple hv_RowCentre, out HTuple hv_ColumnCentre, out HTuple hv_Radius, out HTuple hv_Lengthes)
        {
            HTuple hv_GRRmultiLine = GRRmultiLine;
            HTuple hv_GRRmultiAngle = GRRmultiAngle;


            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_SymbolXLDs = null, ho_Roi_Markaffine = null;
            HObject ho_Region = null, ho_Circle = new HObject(), ho_shouRegionRectangleMeasures = new HObject();

            // Local control variables 


            HTuple hv_CodeRow = new HTuple();


            HTuple hv_CodeColumn = new HTuple(), hv_CodeAngle = new HTuple();
            HTuple hv_Exception = new HTuple(), hv_HomMat2D = new HTuple();
            HTuple hv_Number = new HTuple(), hv_MarkCol1 = new HTuple();
            HTuple hv_MarkRow1 = new HTuple(), hv_Col2 = new HTuple();
            HTuple hv_Row2 = new HTuple(), hv_Col3 = new HTuple();
            HTuple hv_Row3 = new HTuple(), hv_Col4 = new HTuple();
            HTuple hv_Row4 = new HTuple(), hv_lRow1s = new HTuple();
            HTuple hv_lCol1s = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ResultImage);
            HOperatorSet.GenEmptyObj(out ho_SymbolXLDs);
            HOperatorSet.GenEmptyObj(out ho_Roi_Markaffine);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_Circle);
            HOperatorSet.GenEmptyObj(out ho_shouRegionRectangleMeasures);
            try
            {
                ho_ResultImage.Dispose();
                ho_ResultImage = ho_SrcImage.CopyObj(1, -1);
                hv_DecodedDataStrings = "";
                hv_Lengthes = new HTuple();
                hv_Lengthes[0] = -1;
                hv_Lengthes[1] = -1;
                hv_Lengthes[2] = -1;
                hv_Lengthes[3] = -1;
                hv_RowCentre = 0;
                hv_ColumnCentre = 0;
                hv_Radius = 0;
                try
                {
                    hv_RowCentre = new HTuple();
                    hv_ColumnCentre = new HTuple();
                    hv_Radius = new HTuple();
                    hv_Lengthes = new HTuple();
                    hv_DecodedDataStrings = new HTuple();

                    //1¡¢ÕÒ¶þÎ¬Âë
                    ho_SymbolXLDs.Dispose();
                    FindCode(ho_SrcImage, ho_Roi_Code, out ho_SymbolXLDs, hv_DataCodeHandle,
                        out hv_DecodedDataStrings, out hv_CodeRow, out hv_CodeColumn, out hv_CodeAngle);
                    if ((int)(new HTuple(hv_CodeRow.TupleEqual(0))) != 0)
                    {
                        logFile("FindCode fail", hv_Exception);
                        ho_SymbolXLDs.Dispose();
                        ho_Roi_Markaffine.Dispose();
                        ho_Region.Dispose();
                        ho_Circle.Dispose();
                        ho_shouRegionRectangleMeasures.Dispose();

                        return;
                    }

                    //2¡¢ÕÒMarkµã

                    HOperatorSet.VectorAngleToRigid(hv_modelCodeRow, hv_modelCodeCol, hv_modelCodeAngle,
                        hv_CodeRow, hv_CodeColumn, hv_CodeAngle, out hv_HomMat2D);
                    ho_Roi_Markaffine.Dispose();
                    HOperatorSet.AffineTransRegion(ho_Roi_Mark, out ho_Roi_Markaffine, hv_HomMat2D,
                        "nearest_neighbor");

                    ho_Region.Dispose();
                    Find_Mark_Region(ho_SrcImage, ho_Roi_Markaffine, out ho_Region);
                    HOperatorSet.CountObj(ho_Region, out hv_Number);
                    if ((int)(new HTuple(hv_Number.TupleEqual(0))) != 0)
                    {
                        ho_SymbolXLDs.Dispose();
                        ho_Roi_Markaffine.Dispose();
                        ho_Region.Dispose();
                        ho_Circle.Dispose();
                        ho_shouRegionRectangleMeasures.Dispose();
                        // return;
                    }
                    Get_Rectangle1_4Points(ho_Region, out hv_MarkCol1, out hv_MarkRow1, out hv_Col2,
                        out hv_Row2, out hv_Col3, out hv_Row3, out hv_Col4, out hv_Row4);
                    if ((int)(new HTuple(hv_MarkCol1.TupleEqual(-1))) != 0)
                    {
                        ho_SymbolXLDs.Dispose();
                        ho_Roi_Markaffine.Dispose();
                        ho_Region.Dispose();
                        ho_Circle.Dispose();
                        ho_shouRegionRectangleMeasures.Dispose();

                        // return;
                    }
                    //3¡¢ÕÒÔ²ÐÄ
                    //
                    ho_Circle.Dispose();
                    GetCircleAOI(ho_SrcImage, out ho_Circle, out hv_RowCentre, out hv_ColumnCentre, out hv_Radius);
                    if ((int)(new HTuple(hv_RowCentre.TupleEqual(-1))) != 0)
                    {
                        ho_SymbolXLDs.Dispose();
                        ho_Roi_Markaffine.Dispose();
                        ho_Region.Dispose();
                        ho_Circle.Dispose();
                        ho_shouRegionRectangleMeasures.Dispose();

                        return;
                    }

                    //4
                    ho_shouRegionRectangleMeasures.Dispose();
                    //Measuring4Line(ho_SrcImage, hv_GRRmultiLine, hv_GRRmultiAngle, out ho_shouRegionRectangleMeasures, hv_RowCentre,
                    //    hv_ColumnCentre, hv_MarkRow1, hv_MarkCol1, hv_MarkAngle, out hv_Lengthes,
                    //    out hv_lRow1s, out hv_lCol1s);

                    Measuring4Line_Fit(ho_SrcImage, hv_GRRmultiLine, hv_GRRmultiAngle, out ho_shouRegionRectangleMeasures, hv_RowCentre,
                       hv_ColumnCentre, hv_MarkRow1, hv_MarkCol1, hv_MarkAngle, out hv_Lengthes,
                       out hv_lRow1s, out hv_lCol1s);

                    if ((int)(new HTuple((new HTuple(hv_lRow1s.TupleLength())).TupleNotEqual(
                        4))) != 0)
                    {
                        ho_SymbolXLDs.Dispose();
                        ho_Roi_Markaffine.Dispose();
                        ho_Region.Dispose();
                        ho_Circle.Dispose();
                        ho_shouRegionRectangleMeasures.Dispose();

                        // return;
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_shouRegionRectangleMeasures, ho_Region, out ExpTmpOutVar_0
                            );
                        ho_shouRegionRectangleMeasures.Dispose();
                        ho_shouRegionRectangleMeasures = ExpTmpOutVar_0;
                    }
                    //{
                    //    HObject ExpTmpOutVar_0;
                    //    HOperatorSet.ConcatObj(ho_shouRegionRectangleMeasures, ho_Circle, out ExpTmpOutVar_0
                    //        );
                    //    ho_shouRegionRectangleMeasures.Dispose();
                    //    ho_shouRegionRectangleMeasures = ExpTmpOutVar_0;
                    //}

                    hv_Lengthes = hv_Lengthes * Convert.ToDouble(FormProdctPar.UpDownCam1_Pix2MM.Value);

                    ho_ResultImage.Dispose();

                    ho_ResultImage = ho_SrcImage.Clone();
                    if (FormProdctPar.checkBox_show_result_image_cam1.Checked)
                    {
                        createCapAOIResultImage(ho_SrcImage, ho_shouRegionRectangleMeasures, out ho_ResultImage,
                       hv_lRow1s, hv_lCol1s, hv_Lengthes);
                    }


                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("MeasuringCap fail", hv_Exception);
                }

                ho_SymbolXLDs.Dispose();
                ho_Roi_Markaffine.Dispose();
                ho_Region.Dispose();
                ho_Circle.Dispose();
                ho_shouRegionRectangleMeasures.Dispose();




            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_SymbolXLDs.Dispose();
                ho_Roi_Markaffine.Dispose();
                ho_Region.Dispose();
                ho_Circle.Dispose();
                ho_shouRegionRectangleMeasures.Dispose();

                throw HDevExpDefaultException;
            }
        }

        public void Measuring_Line(HObject ho_SrcImage, out HObject ho_Cross1, out HObject ho_Cross2,
            out HObject ho_Rectangle, HTuple hv_Row1, HTuple hv_Col1, HTuple hv_Row2, HTuple hv_Col2,
            out HTuple hv_Length)
        {

            // Local iconic variables 

            HObject ho_ImageOut = null, ho_Image = null;

            // Local control variables 

            HTuple hv_AmplitudeThreshold = new HTuple();
            HTuple hv_RoiWidthLen2 = new HTuple(), hv_LineRowStart_Measure_01_0 = new HTuple();
            HTuple hv_LineColumnStart_Measure_01_0 = new HTuple();
            HTuple hv_LineRowEnd_Measure_01_0 = new HTuple(), hv_LineColumnEnd_Measure_01_0 = new HTuple();
            HTuple hv_TmpCtrl_Row = new HTuple(), hv_TmpCtrl_Column = new HTuple();
            HTuple hv_TmpCtrl_Dr = new HTuple(), hv_TmpCtrl_Dc = new HTuple();
            HTuple hv_TmpCtrl_Phi = new HTuple(), hv_TmpCtrl_Len1 = new HTuple();
            HTuple hv_TmpCtrl_Len2 = new HTuple(), hv_MsrHandle_Measure_01_0 = new HTuple();
            HTuple hv_Row_Measure_01_0 = new HTuple(), hv_Column_Measure_01_0 = new HTuple();
            HTuple hv_Amplitude_Measure_01_0 = new HTuple(), hv_Distance_Measure_01_0 = new HTuple();
            HTuple hv_Sum = new HTuple(), hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Cross1);
            HOperatorSet.GenEmptyObj(out ho_Cross2);
            HOperatorSet.GenEmptyObj(out ho_Rectangle);
            HOperatorSet.GenEmptyObj(out ho_ImageOut);
            HOperatorSet.GenEmptyObj(out ho_Image);
            try
            {

                ho_Cross1.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Cross1);
                ho_Cross2.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Cross2);
                ho_Rectangle.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Rectangle);
                hv_Length = -1;

                try
                {
                    ho_ImageOut.Dispose();
                    ho_ImageOut = ho_SrcImage.CopyObj(1, -1);
                    //Measure 01: Code generated by Measure 01
                    //Measure 01: Prepare measurement
                    hv_AmplitudeThreshold = 50;
                    hv_RoiWidthLen2 = 20;
                    HOperatorSet.SetSystem("int_zooming", "true");
                    //Measure 01: Coordinates for line Measure 01 [0]
                    hv_LineRowStart_Measure_01_0 = hv_Row1.Clone();
                    hv_LineColumnStart_Measure_01_0 = hv_Col1.Clone();
                    hv_LineRowEnd_Measure_01_0 = hv_Row2.Clone();
                    hv_LineColumnEnd_Measure_01_0 = hv_Col2.Clone();
                    //Measure 01: Convert coordinates to rectangle2 type
                    hv_TmpCtrl_Row = 0.5 * (hv_LineRowStart_Measure_01_0 + hv_LineRowEnd_Measure_01_0);
                    hv_TmpCtrl_Column = 0.5 * (hv_LineColumnStart_Measure_01_0 + hv_LineColumnEnd_Measure_01_0);
                    hv_TmpCtrl_Dr = hv_LineRowStart_Measure_01_0 - hv_LineRowEnd_Measure_01_0;
                    hv_TmpCtrl_Dc = hv_LineColumnEnd_Measure_01_0 - hv_LineColumnStart_Measure_01_0;
                    hv_TmpCtrl_Phi = hv_TmpCtrl_Dr.TupleAtan2(hv_TmpCtrl_Dc);
                    hv_TmpCtrl_Len1 = 0.5 * ((((hv_TmpCtrl_Dr * hv_TmpCtrl_Dr) + (hv_TmpCtrl_Dc * hv_TmpCtrl_Dc))).TupleSqrt()
                        );
                    hv_TmpCtrl_Len2 = hv_RoiWidthLen2.Clone();
                    //Measure 01: Create measure for line Measure 01 [0]
                    //Measure 01: Attention: This assumes all images have the same size!
                    HOperatorSet.GenMeasureRectangle2(hv_TmpCtrl_Row, hv_TmpCtrl_Column, hv_TmpCtrl_Phi,
                        hv_TmpCtrl_Len1, hv_TmpCtrl_Len2, 5120, 5120, "nearest_neighbor", out hv_MsrHandle_Measure_01_0);
                    ho_Rectangle.Dispose();
                    HOperatorSet.GenRectangle2(out ho_Rectangle, hv_TmpCtrl_Row, hv_TmpCtrl_Column,
                        hv_TmpCtrl_Phi, hv_TmpCtrl_Len1, hv_TmpCtrl_Len2);
                    //Measure 01: ***************************************************************
                    //Measure 01: * The code which follows is to be executed once / measurement *
                    //Measure 01: ***************************************************************
                    //Measure 01: The image is assumed to be made available in the
                    //Measure 01: variable last displayed in the graphics window
                    ho_Image.Dispose();
                    HOperatorSet.CopyObj(ho_SrcImage, out ho_Image, 1, 1);
                    //Measure 01: Execute measurements
                    HOperatorSet.MeasurePos(ho_Image, hv_MsrHandle_Measure_01_0, 0.8, 33, "all",
                        "all", out hv_Row_Measure_01_0, out hv_Column_Measure_01_0, out hv_Amplitude_Measure_01_0,
                        out hv_Distance_Measure_01_0);
                    //Measure 01: Do something with the results
                    //Measure 01: Clear measure when done
                    HOperatorSet.TupleSum(hv_Distance_Measure_01_0, out hv_Sum);
                    hv_Length = hv_Sum.Clone();
                    ho_Cross1.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_Cross1, hv_Row_Measure_01_0.TupleSelect(
                        0), hv_Column_Measure_01_0.TupleSelect(0), 5, 0.785398);
                    ho_Cross2.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_Cross2, hv_Row_Measure_01_0.TupleSelect(
                        (new HTuple(hv_Row_Measure_01_0.TupleLength())) - 1), hv_Column_Measure_01_0.TupleSelect(
                        (new HTuple(hv_Column_Measure_01_0.TupleLength())) - 1), 5, 0.785398);


                    HOperatorSet.CloseMeasure(hv_MsrHandle_Measure_01_0);
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("MeasureLine fail", hv_Exception);
                }
                ho_ImageOut.Dispose();
                ho_Image.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_ImageOut.Dispose();
                ho_Image.Dispose();

                throw HDevExpDefaultException;
            }
        }

        public void createCapAOIResultImage(HObject ho_SrcImage, HObject ho_BodyRegions,
            out HObject ho_ResultImage, HTuple hv_Rows, HTuple hv_Cols, HTuple hv_Lengthes)
        {
            // Local iconic variables 

            // Local control variables 

            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_WindowHandle = new HTuple(), hv_StringFAI = new HTuple();
            HTuple hv_i = new HTuple(), hv_k = new HTuple(), hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ResultImage);
            try
            {
                ho_ResultImage.Dispose();
                ho_ResultImage = ho_SrcImage.CopyObj(1, -1);

                HOperatorSet.GetImageSize(ho_SrcImage, out hv_Width, out hv_Height);
                HOperatorSet.OpenWindow(0, 0, hv_Width, hv_Height, 0, "buffer", "", out hv_WindowHandle);
                HOperatorSet.SetLineWidth(hv_WindowHandle, 1);
                HOperatorSet.SetColor(hv_WindowHandle, "green");
                HOperatorSet.SetFont(hv_WindowHandle, "-Times New Roman-200-");

                HOperatorSet.DispObj(ho_SrcImage, hv_WindowHandle);
                if ((int)(new HTuple((new HTuple(hv_Lengthes.TupleLength())).TupleNotEqual(
                    4))) != 0)
                {
                    HOperatorSet.SetColor(hv_WindowHandle, "yellow");
                    HOperatorSet.SetTposition(hv_WindowHandle, 0.5 * hv_Height, 0.5 * hv_Width);
                    HOperatorSet.WriteString(hv_WindowHandle, "____Abnomal____");
                    ho_ResultImage.Dispose();
                    HOperatorSet.DumpWindowImage(out ho_ResultImage, hv_WindowHandle);
                    HOperatorSet.CloseWindow(hv_WindowHandle);

                    return;
                }

                HOperatorSet.SetDraw(hv_WindowHandle, "fill");
                hv_StringFAI = new HTuple();
                hv_StringFAI[0] = "AA";
                hv_StringFAI[1] = "BB";
                hv_StringFAI[2] = "CC";
                hv_StringFAI[3] = "DD";

                //hv_Lengthes[0] = hv_Lengthes[0] + (double)FormProdctPar.NDU_AAoff.Value;
                //hv_Lengthes[1] = hv_Lengthes[1] + (double)FormProdctPar.NDU_BBoff.Value;
                //hv_Lengthes[2] = hv_Lengthes[2] + (double)FormProdctPar.NDU_CCoff.Value;
                //hv_Lengthes[3] = hv_Lengthes[3] + (double)FormProdctPar.NDU_DDoff.Value;

                for (hv_i = 0; (int)hv_i <= (int)((new HTuple(hv_Rows.TupleLength())) - 1); hv_i = (int)hv_i + 1)
                {
                    HOperatorSet.SetTposition(hv_WindowHandle, hv_Rows.TupleSelect(hv_i), hv_Cols.TupleSelect(
                        hv_i));
                    HOperatorSet.WriteString(hv_WindowHandle, hv_StringFAI.TupleSelect(hv_i));
                }
                HOperatorSet.SetFont(hv_WindowHandle, "-Times New Roman-200-");
                HOperatorSet.SetDraw(hv_WindowHandle, "margin");
                HOperatorSet.DispObj(ho_BodyRegions, hv_WindowHandle);

                for (hv_k = 0; (int)hv_k <= (int)((new HTuple(hv_Lengthes.TupleLength())) - 1); hv_k = (int)hv_k + 1)
                {

                    HOperatorSet.SetTposition(hv_WindowHandle, 200 * hv_k, 3700);


                    HOperatorSet.WriteString(hv_WindowHandle, ((hv_StringFAI.TupleSelect(hv_k)) + ":  ") + (((hv_Lengthes.TupleSelect(
                        hv_k))).TupleString(".4f")));
                }

                ho_ResultImage.Dispose();
                HOperatorSet.DumpWindowImage(out ho_ResultImage, hv_WindowHandle);
                HOperatorSet.CloseWindow(hv_WindowHandle);

            }
            // catch (Exception) 
            catch (HalconException HDevExpDefaultException1)
            {
                HDevExpDefaultException1.ToHTuple(out hv_Exception);
                logFile("createLocationResultImage fail", hv_Exception);
            }

            return;
        }

        //public void Measuring4Line(HObject ho_Image, out HObject ho_ShowRegions, HTuple hv_RowCentre,
        //    HTuple hv_ColumnCentre, HTuple hv_Row1, HTuple hv_Col1, HTuple hv_std_JD, out HTuple hv_Lengthes,
        //    out HTuple hv_lRow1s, out HTuple hv_lCol1s)
        //{




        //    // Stack for temporary objects 
        //    HObject[] OTemp = new HObject[20];

        //    // Local iconic variables 

        //    HObject ho_Cross11, ho_Cross22, ho_Cross1 = null;
        //    HObject ho_Cross2 = null, ho_Rectangle = null, ho_Cross = null;

        //    // Local control variables 

        //    HTuple hv_PI = null, hv_MultiLine = null, hv_stepAngle = null;
        //    HTuple hv_Phi = new HTuple(), hv_Phi1 = new HTuple(), hv_Index = new HTuple();
        //    HTuple hv_lRow1AA = new HTuple(), hv_lCol1AA = new HTuple();
        //    HTuple hv_lRow2AA = new HTuple(), hv_lCol2AA = new HTuple();
        //    HTuple hv_Length = new HTuple(), hv_Lengths = new HTuple();
        //    HTuple hv_Sum = new HTuple(), hv_lRow1 = new HTuple();
        //    HTuple hv_lCol1 = new HTuple(), hv_lRow2 = new HTuple();
        //    HTuple hv_lCol2 = new HTuple(), hv_Exception = null;
        //    // Initialize local and output iconic variables 
        //    HOperatorSet.GenEmptyObj(out ho_ShowRegions);
        //    HOperatorSet.GenEmptyObj(out ho_Cross11);
        //    HOperatorSet.GenEmptyObj(out ho_Cross22);
        //    HOperatorSet.GenEmptyObj(out ho_Cross1);
        //    HOperatorSet.GenEmptyObj(out ho_Cross2);
        //    HOperatorSet.GenEmptyObj(out ho_Rectangle);
        //    HOperatorSet.GenEmptyObj(out ho_Cross);
        //    hv_lRow1s = new HTuple();
        //    hv_lCol1s = new HTuple();
        //    try
        //    {
        //        hv_PI = 3.1415926;
        //        ho_Cross11.Dispose();
        //        HOperatorSet.GenEmptyObj(out ho_Cross11);
        //        ho_Cross22.Dispose();
        //        HOperatorSet.GenEmptyObj(out ho_Cross22);
        //        ho_ShowRegions.Dispose();
        //        HOperatorSet.GenEmptyObj(out ho_ShowRegions);
        //        hv_Lengthes = new HTuple();
        //        hv_Lengthes[0] = -1;
        //        hv_Lengthes[1] = -1;
        //        hv_Lengthes[2] = -1;
        //        hv_Lengthes[3] = -1;
        //        hv_MultiLine = 9;
        //        hv_stepAngle = (new HTuple(0.6)).TupleRad();
        //        try
        //        {
        //            HOperatorSet.LineOrientation(hv_RowCentre, hv_ColumnCentre, hv_Row1, hv_Col1,
        //                out hv_Phi);

        //            //AA   ¶àÏß²âÁ¿
        //            hv_Phi1 = hv_Phi + ((hv_std_JD * hv_PI) / 180);

        //            if ((int)(new HTuple(hv_MultiLine.TupleNotEqual(0))) != 0)
        //            {
        //                HTuple end_val14 = hv_MultiLine;
        //                HTuple step_val14 = 1;
        //                for (hv_Index = 1; hv_Index.Continue(end_val14, step_val14); hv_Index = hv_Index.TupleAdd(step_val14))
        //                {
        //                    hv_lRow1AA = hv_RowCentre + (2000 * ((((hv_Phi1 - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
        //                        ));
        //                    hv_lCol1AA = hv_ColumnCentre - (2000 * ((((hv_Phi1 - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
        //                        ));
        //                    hv_lRow2AA = hv_RowCentre - (2000 * ((((hv_Phi1 - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
        //                        ));
        //                    hv_lCol2AA = hv_ColumnCentre + (2000 * ((((hv_Phi1 - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
        //                        ));
        //                    ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
        //                    Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
        //                        hv_lRow1AA, hv_lCol1AA, hv_lRow2AA, hv_lCol2AA, out hv_Length);
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
        //                        ho_Cross11.Dispose();
        //                        ho_Cross11 = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
        //                        ho_Cross22.Dispose();
        //                        ho_Cross22 = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    if (hv_Lengths == null)
        //                        hv_Lengths = new HTuple();
        //                    hv_Lengths[hv_Index - 1] = hv_Length;
        //                }
        //                if (hv_Lengthes == null)
        //                    hv_Lengthes = new HTuple();
        //                hv_Lengthes[0] = (hv_Lengths.TupleSum()) / (new HTuple(hv_Lengths.TupleLength()
        //                    ));
        //            }
        //            else
        //            {
        //                hv_lRow1AA = hv_RowCentre + (2000 * (hv_Phi1.TupleSin()));
        //                hv_lCol1AA = hv_ColumnCentre - (2000 * (hv_Phi1.TupleCos()));
        //                hv_lRow2AA = hv_RowCentre - (2000 * (hv_Phi1.TupleSin()));
        //                hv_lCol2AA = hv_ColumnCentre + (2000 * (hv_Phi1.TupleCos()));
        //                ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
        //                Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
        //                    hv_lRow1AA, hv_lCol1AA, hv_lRow2AA, hv_lCol2AA, out hv_Length);
        //                if ((int)(new HTuple((new HTuple(hv_Length.TupleLength())).TupleEqual(0))) != 0)
        //                {
        //                    ho_Cross11.Dispose();
        //                    ho_Cross22.Dispose();
        //                    ho_Cross1.Dispose();
        //                    ho_Cross2.Dispose();
        //                    ho_Rectangle.Dispose();
        //                    ho_Cross.Dispose();

        //                    return;
        //                }
        //                HOperatorSet.TupleSum(hv_Length, out hv_Sum);
        //                hv_Length = hv_Sum.Clone();

        //                if (hv_Lengthes == null)
        //                    hv_Lengthes = new HTuple();
        //                hv_Lengthes[0] = hv_Length;
        //                if (hv_lRow1s == null)
        //                    hv_lRow1s = new HTuple();
        //                hv_lRow1s[0] = hv_lRow1AA;
        //                if (hv_lCol1s == null)
        //                    hv_lCol1s = new HTuple();
        //                hv_lCol1s[0] = hv_lCol1AA;
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
        //                    ho_Cross11.Dispose();
        //                    ho_Cross11 = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
        //                    ho_Cross22.Dispose();
        //                    ho_Cross22 = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0);
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0);
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
        //                        );
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //            }

        //            //*BB
        //            if ((int)(new HTuple(hv_MultiLine.TupleNotEqual(0))) != 0)
        //            {
        //                HTuple end_val52 = hv_MultiLine;
        //                HTuple step_val52 = 1;
        //                for (hv_Index = 1; hv_Index.Continue(end_val52, step_val52); hv_Index = hv_Index.TupleAdd(step_val52))
        //                {
        //                    hv_lRow1 = hv_RowCentre + (2000 * (((((hv_Phi1 + ((new HTuple(45)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
        //                        ));
        //                    hv_lCol1 = hv_ColumnCentre - (2000 * (((((hv_Phi1 + ((new HTuple(45)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
        //                        ));
        //                    hv_lRow2 = hv_RowCentre - (2000 * (((((hv_Phi1 + ((new HTuple(45)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
        //                        ));
        //                    hv_lCol2 = hv_ColumnCentre + (2000 * (((((hv_Phi1 + ((new HTuple(45)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
        //                        ));
        //                    ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
        //                    Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
        //                        hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
        //                        ho_Cross11.Dispose();
        //                        ho_Cross11 = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
        //                        ho_Cross22.Dispose();
        //                        ho_Cross22 = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    if (hv_Lengths == null)
        //                        hv_Lengths = new HTuple();
        //                    hv_Lengths[hv_Index - 1] = hv_Length;
        //                }
        //                if (hv_Lengthes == null)
        //                    hv_Lengthes = new HTuple();
        //                hv_Lengthes[1] = (hv_Lengths.TupleSum()) / (new HTuple(hv_Lengths.TupleLength()
        //                    ));
        //                if (hv_lRow1s == null)
        //                    hv_lRow1s = new HTuple();
        //                hv_lRow1s[1] = hv_lRow1;
        //                if (hv_lCol1s == null)
        //                    hv_lCol1s = new HTuple();
        //                hv_lCol1s[1] = hv_lCol1;
        //            }
        //            else
        //            {
        //                hv_lRow1 = hv_RowCentre + (2000 * (((hv_Phi1 + ((new HTuple(45)).TupleRad()))).TupleSin()
        //                    ));
        //                hv_lCol1 = hv_ColumnCentre - (2000 * (((hv_Phi1 + ((new HTuple(45)).TupleRad()
        //                    ))).TupleCos()));
        //                hv_lRow2 = hv_RowCentre - (2000 * (((hv_Phi1 + ((new HTuple(45)).TupleRad()))).TupleSin()
        //                    ));
        //                hv_lCol2 = hv_ColumnCentre + (2000 * (((hv_Phi1 + ((new HTuple(45)).TupleRad()
        //                    ))).TupleCos()));
        //                ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
        //                Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
        //                    hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);
        //                if ((int)(new HTuple((new HTuple(hv_Length.TupleLength())).TupleEqual(0))) != 0)
        //                {
        //                    ho_Cross11.Dispose();
        //                    ho_Cross22.Dispose();
        //                    ho_Cross1.Dispose();
        //                    ho_Cross2.Dispose();
        //                    ho_Rectangle.Dispose();
        //                    ho_Cross.Dispose();

        //                    return;
        //                }
        //                HOperatorSet.TupleSum(hv_Length, out hv_Sum);
        //                hv_Length = hv_Sum.Clone();

        //                if (hv_Lengthes == null)
        //                    hv_Lengthes = new HTuple();
        //                hv_Lengthes[1] = hv_Length;
        //                if (hv_lRow1s == null)
        //                    hv_lRow1s = new HTuple();
        //                hv_lRow1s[1] = hv_lRow1;
        //                if (hv_lCol1s == null)
        //                    hv_lCol1s = new HTuple();
        //                hv_lCol1s[1] = hv_lCol1;
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
        //                    ho_Cross11.Dispose();
        //                    ho_Cross11 = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
        //                    ho_Cross22.Dispose();
        //                    ho_Cross22 = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0);
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0);
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
        //                        );
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //            }

        //            //*CC
        //            if ((int)(new HTuple(hv_MultiLine.TupleNotEqual(0))) != 0)
        //            {
        //                HTuple end_val92 = hv_MultiLine;
        //                HTuple step_val92 = 1;
        //                for (hv_Index = 1; hv_Index.Continue(end_val92, step_val92); hv_Index = hv_Index.TupleAdd(step_val92))
        //                {
        //                    hv_lRow1 = hv_RowCentre + (2000 * (((((hv_Phi1 + ((new HTuple(90)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
        //                        ));
        //                    hv_lCol1 = hv_ColumnCentre - (2000 * (((((hv_Phi1 + ((new HTuple(90)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
        //                        ));
        //                    hv_lRow2 = hv_RowCentre - (2000 * (((((hv_Phi1 + ((new HTuple(90)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
        //                        ));
        //                    hv_lCol2 = hv_ColumnCentre + (2000 * (((((hv_Phi1 + ((new HTuple(90)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
        //                        ));
        //                    ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
        //                    Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
        //                        hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
        //                        ho_Cross11.Dispose();
        //                        ho_Cross11 = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
        //                        ho_Cross22.Dispose();
        //                        ho_Cross22 = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    if (hv_Lengths == null)
        //                        hv_Lengths = new HTuple();
        //                    hv_Lengths[hv_Index - 1] = hv_Length;
        //                }
        //                if (hv_Lengthes == null)
        //                    hv_Lengthes = new HTuple();
        //                hv_Lengthes[2] = (hv_Lengths.TupleSum()) / (new HTuple(hv_Lengths.TupleLength()
        //                    ));
        //                if (hv_lRow1s == null)
        //                    hv_lRow1s = new HTuple();
        //                hv_lRow1s[2] = hv_lRow1;
        //                if (hv_lCol1s == null)
        //                    hv_lCol1s = new HTuple();
        //                hv_lCol1s[2] = hv_lCol1;
        //            }
        //            else
        //            {
        //                hv_lRow1 = hv_RowCentre + (2000 * (((hv_Phi1 + ((new HTuple(90)).TupleRad()))).TupleSin()
        //                    ));
        //                hv_lCol1 = hv_ColumnCentre - (2000 * (((hv_Phi1 + ((new HTuple(90)).TupleRad()
        //                    ))).TupleCos()));
        //                hv_lRow2 = hv_RowCentre - (2000 * (((hv_Phi1 + ((new HTuple(90)).TupleRad()))).TupleSin()
        //                    ));
        //                hv_lCol2 = hv_ColumnCentre + (2000 * (((hv_Phi1 + ((new HTuple(90)).TupleRad()
        //                    ))).TupleCos()));
        //                ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
        //                Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
        //                    hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);
        //                if ((int)(new HTuple((new HTuple(hv_Length.TupleLength())).TupleEqual(0))) != 0)
        //                {
        //                    ho_Cross11.Dispose();
        //                    ho_Cross22.Dispose();
        //                    ho_Cross1.Dispose();
        //                    ho_Cross2.Dispose();
        //                    ho_Rectangle.Dispose();
        //                    ho_Cross.Dispose();

        //                    return;
        //                }
        //                HOperatorSet.TupleSum(hv_Length, out hv_Sum);
        //                hv_Length = hv_Sum.Clone();

        //                if (hv_Lengthes == null)
        //                    hv_Lengthes = new HTuple();
        //                hv_Lengthes[2] = hv_Length;
        //                if (hv_lRow1s == null)
        //                    hv_lRow1s = new HTuple();
        //                hv_lRow1s[2] = hv_lRow1;
        //                if (hv_lCol1s == null)
        //                    hv_lCol1s = new HTuple();
        //                hv_lCol1s[2] = hv_lCol1;
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
        //                    ho_Cross11.Dispose();
        //                    ho_Cross11 = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
        //                    ho_Cross22.Dispose();
        //                    ho_Cross22 = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0);
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0);
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
        //                        );
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //            }
        //            //*DD
        //            if ((int)(new HTuple(hv_MultiLine.TupleNotEqual(0))) != 0)
        //            {
        //                HTuple end_val131 = hv_MultiLine;
        //                HTuple step_val131 = 1;
        //                for (hv_Index = 1; hv_Index.Continue(end_val131, step_val131); hv_Index = hv_Index.TupleAdd(step_val131))
        //                {
        //                    hv_lRow1 = hv_RowCentre + (2000 * (((((hv_Phi1 + ((new HTuple(135)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
        //                        ));
        //                    hv_lCol1 = hv_ColumnCentre - (2000 * (((((hv_Phi1 + ((new HTuple(135)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
        //                        ));
        //                    hv_lRow2 = hv_RowCentre - (2000 * (((((hv_Phi1 + ((new HTuple(135)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
        //                        ));
        //                    hv_lCol2 = hv_ColumnCentre + (2000 * (((((hv_Phi1 + ((new HTuple(135)).TupleRad()
        //                        )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
        //                        ));
        //                    ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
        //                    Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
        //                        hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
        //                        ho_Cross11.Dispose();
        //                        ho_Cross11 = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
        //                        ho_Cross22.Dispose();
        //                        ho_Cross22 = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    {
        //                        HObject ExpTmpOutVar_0;
        //                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
        //                            );
        //                        ho_ShowRegions.Dispose();
        //                        ho_ShowRegions = ExpTmpOutVar_0;
        //                    }
        //                    if (hv_Lengths == null)
        //                        hv_Lengths = new HTuple();
        //                    hv_Lengths[hv_Index - 1] = hv_Length;
        //                }
        //                if (hv_Lengthes == null)
        //                    hv_Lengthes = new HTuple();
        //                hv_Lengthes[3] = (hv_Lengths.TupleSum()) / (new HTuple(hv_Lengths.TupleLength()
        //                    ));
        //                if (hv_lRow1s == null)
        //                    hv_lRow1s = new HTuple();
        //                hv_lRow1s[3] = hv_lRow1;
        //                if (hv_lCol1s == null)
        //                    hv_lCol1s = new HTuple();
        //                hv_lCol1s[3] = hv_lCol1;
        //            }
        //            else
        //            {
        //                hv_lRow1 = hv_RowCentre + (2000 * (((hv_Phi1 + ((new HTuple(135)).TupleRad()))).TupleSin()
        //                    ));
        //                hv_lCol1 = hv_ColumnCentre - (2000 * (((hv_Phi1 + ((new HTuple(135)).TupleRad()
        //                    ))).TupleCos()));
        //                hv_lRow2 = hv_RowCentre - (2000 * (((hv_Phi1 + ((new HTuple(135)).TupleRad()))).TupleSin()
        //                    ));
        //                hv_lCol2 = hv_ColumnCentre + (2000 * (((hv_Phi1 + ((new HTuple(135)).TupleRad()
        //                    ))).TupleCos()));
        //                ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
        //                Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
        //                    hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);
        //                if ((int)(new HTuple((new HTuple(hv_Length.TupleLength())).TupleEqual(0))) != 0)
        //                {
        //                    ho_Cross11.Dispose();
        //                    ho_Cross22.Dispose();
        //                    ho_Cross1.Dispose();
        //                    ho_Cross2.Dispose();
        //                    ho_Rectangle.Dispose();
        //                    ho_Cross.Dispose();

        //                    return;
        //                }
        //                HOperatorSet.TupleSum(hv_Length, out hv_Sum);
        //                hv_Length = hv_Sum.Clone();

        //                if (hv_Lengthes == null)
        //                    hv_Lengthes = new HTuple();
        //                hv_Lengthes[3] = hv_Length;
        //                if (hv_lRow1s == null)
        //                    hv_lRow1s = new HTuple();
        //                hv_lRow1s[3] = hv_lRow1;
        //                if (hv_lCol1s == null)
        //                    hv_lCol1s = new HTuple();
        //                hv_lCol1s[3] = hv_lCol1;
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
        //                    ho_Cross11.Dispose();
        //                    ho_Cross11 = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
        //                    ho_Cross22.Dispose();
        //                    ho_Cross22 = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0);
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0);
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //                {
        //                    HObject ExpTmpOutVar_0;
        //                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
        //                        );
        //                    ho_ShowRegions.Dispose();
        //                    ho_ShowRegions = ExpTmpOutVar_0;
        //                }
        //            }

        //            //ÏÔÊ¾markµã
        //            ho_Cross.Dispose();
        //            HOperatorSet.GenCrossContourXld(out ho_Cross, hv_Row1, hv_Col1, 70, 0.5);
        //            {
        //                HObject ExpTmpOutVar_0;
        //                HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross, out ExpTmpOutVar_0);
        //                ho_ShowRegions.Dispose();
        //                ho_ShowRegions = ExpTmpOutVar_0;
        //            }

        //        }
        //        // catch (Exception) 
        //        catch (HalconException HDevExpDefaultException1)
        //        {
        //            HDevExpDefaultException1.ToHTuple(out hv_Exception);
        //            logFile("Measuring4Line fail", hv_Exception);
        //        }


        //        ho_Cross11.Dispose();
        //        ho_Cross22.Dispose();
        //        ho_Cross1.Dispose();
        //        ho_Cross2.Dispose();
        //        ho_Rectangle.Dispose();
        //        ho_Cross.Dispose();

        //        return;
        //    }
        //    catch (HalconException HDevExpDefaultException)
        //    {
        //        ho_Cross11.Dispose();
        //        ho_Cross22.Dispose();
        //        ho_Cross1.Dispose();
        //        ho_Cross2.Dispose();
        //        ho_Rectangle.Dispose();
        //        ho_Cross.Dispose();

        //        throw HDevExpDefaultException;
        //    }
        //}

        public void Measuring4Line(HObject ho_Image, HTuple hv_MultiLine, HTuple hv_stepAngle, out HObject ho_ShowRegions, HTuple hv_RowCentre,
        HTuple hv_ColumnCentre, HTuple hv_Row1, HTuple hv_Col1, HTuple hv_std_JD, out HTuple hv_Lengthes,
        out HTuple hv_lRow1s, out HTuple hv_lCol1s)
        {

            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_Cross11, ho_Cross22, ho_Cross1 = null;
            HObject ho_Cross2 = null, ho_Rectangle = null, ho_Cross = null;

            // Local control variables 

            HTuple hv_PI = null;
            HTuple hv_Phi = new HTuple(), hv_Phi1 = new HTuple(), hv_FAI = new HTuple();
            HTuple hv_Index = new HTuple(), hv_lRow1 = new HTuple();
            HTuple hv_lCol1 = new HTuple(), hv_lRow2 = new HTuple();
            HTuple hv_lCol2 = new HTuple(), hv_Length = new HTuple();
            HTuple hv_Lengths = new HTuple(), hv_LengthsSorted = new HTuple();
            HTuple hv_Sum = new HTuple(), hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ShowRegions);
            HOperatorSet.GenEmptyObj(out ho_Cross11);
            HOperatorSet.GenEmptyObj(out ho_Cross22);
            HOperatorSet.GenEmptyObj(out ho_Cross1);
            HOperatorSet.GenEmptyObj(out ho_Cross2);
            HOperatorSet.GenEmptyObj(out ho_Rectangle);
            HOperatorSet.GenEmptyObj(out ho_Cross);
            hv_lRow1s = new HTuple();
            hv_lCol1s = new HTuple();
            try
            {
                hv_PI = 3.14159265;
                ho_Cross11.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Cross11);
                ho_Cross22.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Cross22);
                ho_ShowRegions.Dispose();
                HOperatorSet.GenEmptyObj(out ho_ShowRegions);
                hv_Lengthes = new HTuple();
                hv_Lengthes[0] = -1;
                hv_Lengthes[1] = -1;
                hv_Lengthes[2] = -1;
                hv_Lengthes[3] = -1;
                hv_stepAngle = (new HTuple(hv_stepAngle)).TupleRad();
                try
                {
                    HOperatorSet.LineOrientation(hv_RowCentre, hv_ColumnCentre, hv_Row1, hv_Col1,
                        out hv_Phi);
                    //¶àÏß²âÁ¿
                    hv_Phi1 = hv_Phi + ((hv_std_JD * hv_PI) / 180);
                    for (hv_FAI = 0; (int)hv_FAI <= 3; hv_FAI = (int)hv_FAI + 1)
                    {
                        if ((int)(new HTuple(hv_MultiLine.TupleNotEqual(0))) != 0)
                        {
                            HTuple end_val13 = hv_MultiLine;
                            HTuple step_val13 = 1;
                            for (hv_Index = 1; hv_Index.Continue(end_val13, step_val13); hv_Index = hv_Index.TupleAdd(step_val13))
                            {
                                hv_lRow1 = hv_RowCentre + (2000 * (((((hv_Phi1 + (((hv_FAI * 45)).TupleRad()
                                    )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
                                    ));
                                hv_lCol1 = hv_ColumnCentre - (2000 * (((((hv_Phi1 + (((hv_FAI * 45)).TupleRad()
                                    )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
                                    ));
                                hv_lRow2 = hv_RowCentre - (2000 * (((((hv_Phi1 + (((hv_FAI * 45)).TupleRad()
                                    )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
                                    ));
                                hv_lCol2 = hv_ColumnCentre + (2000 * (((((hv_Phi1 + (((hv_FAI * 45)).TupleRad()
                                    )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
                                    ));
                                ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
                                Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
                                    hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);

                                if ((int)((new HTuple(hv_Index.TupleEqual(1))).TupleAnd(new HTuple((new HTuple(hv_Length.TupleLength()
                                    )).TupleEqual(0)))) != 0)
                                {
                                    if (hv_Lengths == null)
                                        hv_Lengths = new HTuple();
                                    hv_Lengths[0] = -1;
                                }
                                if ((int)((new HTuple(hv_Index.TupleNotEqual(1))).TupleAnd(new HTuple((new HTuple(hv_Length.TupleLength()
                                    )).TupleEqual(0)))) != 0)
                                {
                                    hv_Length = hv_Lengths[0];
                                }

                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
                                    ho_Cross11.Dispose();
                                    ho_Cross11 = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
                                    ho_Cross22.Dispose();
                                    ho_Cross22 = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                                if (hv_Lengths == null)
                                    hv_Lengths = new HTuple();
                                hv_Lengths[hv_Index - 1] = hv_Length;
                            }

                            if ((int)(new HTuple(((hv_Lengths.TupleSelect(0))).TupleEqual(-1))) != 0)
                            {
                                if (hv_Lengths == null)
                                    hv_Lengths = new HTuple();
                                hv_Lengths[0] = hv_Lengths.TupleSelect(1);
                            }
                            HOperatorSet.TupleSort(hv_Lengths, out hv_LengthsSorted);
                            if (hv_Lengthes == null)
                                hv_Lengthes = new HTuple();
                            // hv_Lengthes[hv_FAI] = hv_LengthsSorted.TupleSelect((new HTuple(hv_LengthsSorted.TupleLength())) / 2);
                            hv_Lengthes[hv_FAI] = (hv_Lengths.TupleSum()) / (new HTuple(hv_Lengths.TupleLength()));

                            if (hv_lRow1s == null)
                                hv_lRow1s = new HTuple();
                            hv_lRow1s[hv_FAI] = hv_lRow1;
                            if (hv_lCol1s == null)
                                hv_lCol1s = new HTuple();
                            hv_lCol1s[hv_FAI] = hv_lCol1;

                        }
                        else
                        {
                            hv_lRow1 = hv_RowCentre + (2000 * (((hv_Phi1 + (((hv_FAI * 45)).TupleRad()))).TupleSin()
                                ));
                            hv_lCol1 = hv_ColumnCentre - (2000 * (((hv_Phi1 + (((hv_FAI * 45)).TupleRad()
                                ))).TupleCos()));
                            hv_lRow2 = hv_RowCentre - (2000 * (((hv_Phi1 + (((hv_FAI * 45)).TupleRad()))).TupleSin()
                                ));
                            hv_lCol2 = hv_ColumnCentre + (2000 * (((hv_Phi1 + (((hv_FAI * 45)).TupleRad()
                                ))).TupleCos()));
                            ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
                            Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
                                hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);
                            if ((int)(new HTuple((new HTuple(hv_Length.TupleLength())).TupleEqual(
                                0))) != 0)
                            {
                                ho_Cross11.Dispose();
                                ho_Cross22.Dispose();
                                ho_Cross1.Dispose();
                                ho_Cross2.Dispose();
                                ho_Rectangle.Dispose();
                                ho_Cross.Dispose();

                                return;
                            }
                            HOperatorSet.TupleSum(hv_Length, out hv_Sum);
                            hv_Length = hv_Sum.Clone();

                            if (hv_Lengthes == null)
                                hv_Lengthes = new HTuple();
                            hv_Lengthes[hv_FAI] = hv_Length;
                            if (hv_lRow1s == null)
                                hv_lRow1s = new HTuple();
                            hv_lRow1s[hv_FAI] = hv_lRow1;
                            if (hv_lCol1s == null)
                                hv_lCol1s = new HTuple();
                            hv_lCol1s[hv_FAI] = hv_lCol1;
                            {
                                HObject ExpTmpOutVar_0;
                                HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
                                ho_Cross11.Dispose();
                                ho_Cross11 = ExpTmpOutVar_0;
                            }
                            {
                                HObject ExpTmpOutVar_0;
                                HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
                                ho_Cross22.Dispose();
                                ho_Cross22 = ExpTmpOutVar_0;
                            }
                            {
                                HObject ExpTmpOutVar_0;
                                HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0
                                    );
                                ho_ShowRegions.Dispose();
                                ho_ShowRegions = ExpTmpOutVar_0;
                            }
                            {
                                HObject ExpTmpOutVar_0;
                                HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0
                                    );
                                ho_ShowRegions.Dispose();
                                ho_ShowRegions = ExpTmpOutVar_0;
                            }
                            {
                                HObject ExpTmpOutVar_0;
                                HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
                                    );
                                ho_ShowRegions.Dispose();
                                ho_ShowRegions = ExpTmpOutVar_0;
                            }
                        }

                    }



                    //ÏÔÊ¾markµã
                    ho_Cross.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_Cross, hv_Row1, hv_Col1, 70, 0.5);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross, out ExpTmpOutVar_0);
                        ho_ShowRegions.Dispose();
                        ho_ShowRegions = ExpTmpOutVar_0;
                    }

                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("Measuring4Line fail", hv_Exception);
                }


                ho_Cross11.Dispose();
                ho_Cross22.Dispose();
                ho_Cross1.Dispose();
                ho_Cross2.Dispose();
                ho_Rectangle.Dispose();
                ho_Cross.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Cross11.Dispose();
                ho_Cross22.Dispose();
                ho_Cross1.Dispose();
                ho_Cross2.Dispose();
                ho_Rectangle.Dispose();
                ho_Cross.Dispose();

                throw HDevExpDefaultException;
            }
        }

        public void Measuring4Line_Fit(HObject ho_Image, HTuple hv_MultiLine, HTuple hv_stepAngle, out HObject ho_ShowRegions, HTuple hv_RowCentre,
        HTuple hv_ColumnCentre, HTuple hv_MarkRow, HTuple hv_MarkCol, HTuple hv_std_JD,
        out HTuple hv_Lengthes, out HTuple hv_lRow1s, out HTuple hv_lCol1s)
        {




            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_Cross11, ho_Cross22, ho_Cross1 = null;
            HObject ho_Cross2 = null, ho_Rectangle = null, ho_Cross = null;

            // Local control variables 

            HTuple hv_PI = null, hv_SubEdgMeasure = null;
            HTuple hv_Phi = new HTuple();
            HTuple hv_Phi1 = new HTuple(), hv_FAI = new HTuple(), hv_Index = new HTuple();
            HTuple hv_lRow1 = new HTuple(), hv_lCol1 = new HTuple();
            HTuple hv_lRow2 = new HTuple(), hv_lCol2 = new HTuple();
            HTuple hv_Length = new HTuple(), hv_Lengths = new HTuple();
            HTuple hv_LengthsSorted = new HTuple(), hv_Sum = new HTuple();
            HTuple hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ShowRegions);
            HOperatorSet.GenEmptyObj(out ho_Cross11);
            HOperatorSet.GenEmptyObj(out ho_Cross22);
            HOperatorSet.GenEmptyObj(out ho_Cross1);
            HOperatorSet.GenEmptyObj(out ho_Cross2);
            HOperatorSet.GenEmptyObj(out ho_Rectangle);
            HOperatorSet.GenEmptyObj(out ho_Cross);
            hv_lRow1s = new HTuple();
            hv_lCol1s = new HTuple();
            try
            {
                hv_PI = 3.1415926;
                ho_Cross11.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Cross11);
                ho_Cross22.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Cross22);
                ho_ShowRegions.Dispose();
                HOperatorSet.GenEmptyObj(out ho_ShowRegions);
                hv_SubEdgMeasure = 0;
                hv_Lengthes = new HTuple();
                hv_Lengthes[0] = -1;
                hv_Lengthes[1] = -1;
                hv_Lengthes[2] = -1;
                hv_Lengthes[3] = -1;
                // hv_MultiLine = hv_MultiLine;
                hv_stepAngle = (new HTuple(0.5)).TupleRad();
                try
                {
                    HOperatorSet.LineOrientation(hv_RowCentre, hv_ColumnCentre, hv_MarkRow, hv_MarkCol,
                        out hv_Phi);

                    //AA   ¶àÏß²âÁ¿
                    hv_Phi1 = hv_Phi + ((hv_std_JD * hv_PI) / 180);
                    for (hv_FAI = 0; (int)hv_FAI <= 3; hv_FAI = (int)hv_FAI + 1)
                    {
                        if ((int)(new HTuple(hv_MultiLine.TupleNotEqual(0))) != 0)
                        {
                            HTuple end_val15 = hv_MultiLine;
                            HTuple step_val15 = 1;
                            for (hv_Index = 1; hv_Index.Continue(end_val15, step_val15); hv_Index = hv_Index.TupleAdd(step_val15))
                            {
                                hv_lRow1 = hv_RowCentre + (1980 * (((((hv_Phi1 + (((45 * hv_FAI)).TupleRad()
                                    )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
                                    ));
                                hv_lCol1 = hv_ColumnCentre - (1980 * (((((hv_Phi1 + (((45 * hv_FAI)).TupleRad()
                                    )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
                                    ));
                                hv_lRow2 = hv_RowCentre - (1980 * (((((hv_Phi1 + (((45 * hv_FAI)).TupleRad()
                                    )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleSin()
                                    ));
                                hv_lCol2 = hv_ColumnCentre + (1980 * (((((hv_Phi1 + (((45 * hv_FAI)).TupleRad()
                                    )) - (((hv_MultiLine - 1) / 2) * hv_stepAngle)) + (hv_Index * hv_stepAngle))).TupleCos()
                                    ));
                                ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
                                Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
                                    hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);

                                if ((int)((new HTuple(hv_Index.TupleEqual(1))).TupleAnd(new HTuple((new HTuple(hv_Length.TupleLength()
                                    )).TupleEqual(0)))) != 0)
                                {
                                    if (hv_Lengths == null)
                                        hv_Lengths = new HTuple();
                                    hv_Lengths[0] = -1;
                                }
                                if ((int)((new HTuple(hv_Index.TupleNotEqual(1))).TupleAnd(new HTuple((new HTuple(hv_Length.TupleLength()
                                    )).TupleEqual(0)))) != 0)
                                {
                                    hv_Length = hv_Lengths[0];
                                }

                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
                                    ho_Cross11.Dispose();
                                    ho_Cross11 = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
                                    ho_Cross22.Dispose();
                                    ho_Cross22 = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                                if (hv_Lengths == null)
                                    hv_Lengths = new HTuple();
                                hv_Lengths[hv_Index - 1] = hv_Length;
                            }

                            if ((int)(new HTuple(((hv_Lengths.TupleSelect(0))).TupleEqual(-1))) != 0)
                            {
                                if (hv_Lengths == null)
                                    hv_Lengths = new HTuple();
                                hv_Lengths[0] = hv_Lengths.TupleSelect(1);
                            }

                            HOperatorSet.TupleSort(hv_Lengths, out hv_LengthsSorted);
                            if (hv_Lengthes == null)
                                hv_Lengthes = new HTuple();
                            hv_Lengthes[hv_FAI] = hv_LengthsSorted.TupleSelect((new HTuple(hv_LengthsSorted.TupleLength()
                                )) / 2);

                            if (hv_Lengthes == null)
                                hv_Lengthes = new HTuple();
                            hv_Lengthes[hv_FAI] = (hv_Lengths.TupleSum()) / (new HTuple(hv_Lengths.TupleLength()
                                ));
                            if (hv_lRow1s == null)
                                hv_lRow1s = new HTuple();
                            hv_lRow1s[hv_FAI] = hv_lRow1;
                            if (hv_lCol1s == null)
                                hv_lCol1s = new HTuple();
                            hv_lCol1s[hv_FAI] = hv_lCol1;
                        }
                        else
                        {
                            //²ÉÓÃÕÒ±ßÔÙÄâºÏ±ßÔµÇóÖ±Ïß½»µã
                            if ((int)(new HTuple(hv_SubEdgMeasure.TupleEqual(1))) != 0)
                            {



                            }
                            else
                            {
                                hv_lRow1 = hv_RowCentre + (1980 * (((hv_Phi1 + (((45 * hv_FAI)).TupleRad()))).TupleSin()
                                    ));
                                hv_lCol1 = hv_ColumnCentre - (1980 * (((hv_Phi1 + (((45 * hv_FAI)).TupleRad()
                                    ))).TupleCos()));
                                hv_lRow2 = hv_RowCentre - (1980 * (((hv_Phi1 + (((45 * hv_FAI)).TupleRad()))).TupleSin()
                                    ));
                                hv_lCol2 = hv_ColumnCentre + (1980 * (((hv_Phi1 + (((45 * hv_FAI)).TupleRad()
                                    ))).TupleCos()));


                                ho_Cross1.Dispose(); ho_Cross2.Dispose(); ho_Rectangle.Dispose();
                                Measuring_Line(ho_Image, out ho_Cross1, out ho_Cross2, out ho_Rectangle,
                                    hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2, out hv_Length);
                                if ((int)(new HTuple((new HTuple(hv_Length.TupleLength())).TupleEqual(
                                    0))) != 0)
                                {
                                    ho_Cross11.Dispose();
                                    ho_Cross22.Dispose();
                                    ho_Cross1.Dispose();
                                    ho_Cross2.Dispose();
                                    ho_Rectangle.Dispose();
                                    ho_Cross.Dispose();

                                    return;
                                }
                                HOperatorSet.TupleSum(hv_Length, out hv_Sum);
                                hv_Length = hv_Sum.Clone();

                                if (hv_Lengthes == null)
                                    hv_Lengthes = new HTuple();
                                hv_Lengthes[hv_FAI] = hv_Length;
                                if (hv_lRow1s == null)
                                    hv_lRow1s = new HTuple();
                                hv_lRow1s[hv_FAI] = hv_lRow1;
                                if (hv_lCol1s == null)
                                    hv_lCol1s = new HTuple();
                                hv_lCol1s[hv_FAI] = hv_lCol1;
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_Cross11, ho_Cross1, out ExpTmpOutVar_0);
                                    ho_Cross11.Dispose();
                                    ho_Cross11 = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_Cross22, ho_Cross2, out ExpTmpOutVar_0);
                                    ho_Cross22.Dispose();
                                    ho_Cross22 = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross11, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross22, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ShowRegions, ho_Rectangle, out ExpTmpOutVar_0
                                        );
                                    ho_ShowRegions.Dispose();
                                    ho_ShowRegions = ExpTmpOutVar_0;
                                }
                            }

                        }
                    }


                    //ÏÔÊ¾markµã
                    ho_Cross.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_Cross, hv_MarkRow, hv_MarkCol, 70,
                        0.5);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_ShowRegions, ho_Cross, out ExpTmpOutVar_0);
                        ho_ShowRegions.Dispose();
                        ho_ShowRegions = ExpTmpOutVar_0;
                    }

                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("Measuring4Line fail", hv_Exception);
                }


                ho_Cross11.Dispose();
                ho_Cross22.Dispose();
                ho_Cross1.Dispose();
                ho_Cross2.Dispose();
                ho_Rectangle.Dispose();
                ho_Cross.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Cross11.Dispose();
                ho_Cross22.Dispose();
                ho_Cross1.Dispose();
                ho_Cross2.Dispose();
                ho_Rectangle.Dispose();
                ho_Cross.Dispose();

                throw HDevExpDefaultException;
            }
        }

        public void get_CodeAngle(HObject ho_QrXLD, out HTuple hv_CodeAngle, out HTuple hv_CodeRow,
            out HTuple hv_CodeColumn)
        {



            // Local iconic variables 

            HObject ho_Region = null, ho_RegionMoved = null;
            HObject ho_RegionIntersection = null, ho_ConnectedRegions = null;
            HObject ho_SelectedRegions = null;

            // Local control variables 

            HTuple hv_Area = new HTuple(), hv_Row6 = new HTuple();
            HTuple hv_Column3 = new HTuple(), hv_Length1 = new HTuple();
            HTuple hv_Length2 = new HTuple(), hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_RegionMoved);
            HOperatorSet.GenEmptyObj(out ho_RegionIntersection);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
            try
            {

                hv_CodeAngle = -100;
                hv_CodeRow = -1;
                hv_CodeColumn = -1;
                try
                {
                    ho_Region.Dispose();
                    HOperatorSet.GenRegionContourXld(ho_QrXLD, out ho_Region, "filled");
                    HOperatorSet.AreaCenter(ho_Region, out hv_Area, out hv_CodeRow, out hv_CodeColumn);
                    ho_RegionMoved.Dispose();
                    HOperatorSet.MoveRegion(ho_Region, out ho_RegionMoved, -100, 0);
                    ho_RegionIntersection.Dispose();
                    HOperatorSet.Intersection(ho_Region, ho_RegionMoved, out ho_RegionIntersection
                        );
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_RegionIntersection, out ho_ConnectedRegions);
                    ho_SelectedRegions.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_SelectedRegions,
                        "max_area", 70);
                    HOperatorSet.SmallestRectangle2(ho_SelectedRegions, out hv_Row6, out hv_Column3,
                        out hv_CodeAngle, out hv_Length1, out hv_Length2);
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("get_QrAngle fail", hv_Exception);
                }
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Region.Dispose();
                ho_RegionMoved.Dispose();
                ho_RegionIntersection.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_SelectedRegions.Dispose();

                throw HDevExpDefaultException;
            }
        }

        //******
        public void FindCode(HObject ho_SrcImage, HObject ho_Roi, out HObject ho_SymbolXLDs,
        HTuple hv_DataCodeHandle, out HTuple hv_DecodedDataStrings, out HTuple hv_CodeRow,
        out HTuple hv_CodeColumn, out HTuple hv_CodeAngle)
        {




            // Local iconic variables 

            HObject ho_ImageReduced = null, ho_ImageScaled = null;

            // Local control variables 

            HTuple hv_Area = new HTuple(), hv_Row = new HTuple();
            HTuple hv_Column = new HTuple(), hv_Min = new HTuple();
            HTuple hv_Max = new HTuple(), hv_Range = new HTuple();
            HTuple hv_Mult = new HTuple(), hv_Add = new HTuple(), hv_ResultHandles = new HTuple();
            HTuple hv_Number = new HTuple(), hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_SymbolXLDs);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced);
            HOperatorSet.GenEmptyObj(out ho_ImageScaled);
            try
            {

                hv_DecodedDataStrings = "000000";
                hv_CodeAngle = 0;
                hv_CodeRow = 0;
                hv_CodeColumn = 0;
                try
                {

                    HOperatorSet.AreaCenter(ho_Roi, out hv_Area, out hv_Row, out hv_Column);
                    if ((int)(new HTuple(hv_Area.TupleGreater(0))) != 0)
                    {
                        ho_ImageReduced.Dispose();
                        HOperatorSet.ReduceDomain(ho_SrcImage, ho_Roi, out ho_ImageReduced);
                    }
                    else
                    {
                        ho_ImageReduced.Dispose();
                        ho_ImageReduced = ho_SrcImage.CopyObj(1, -1);
                    }
                    HOperatorSet.MinMaxGray(ho_ImageReduced, ho_ImageReduced, 0, out hv_Min,
                        out hv_Max, out hv_Range);
                    hv_Mult = 255 / (hv_Max - hv_Min);
                    hv_Add = (-hv_Mult) * hv_Min;
                    ho_ImageScaled.Dispose();
                    HOperatorSet.ScaleImage(ho_ImageReduced, out ho_ImageScaled, hv_Mult, hv_Add);

                    ho_SymbolXLDs.Dispose();

                    HOperatorSet.FindDataCode2d(ho_ImageScaled, out ho_SymbolXLDs, FormMain.ECC200CodeHandle,
                        new HTuple(), new HTuple(), out hv_ResultHandles, out hv_DecodedDataStrings);

                    HOperatorSet.CountObj(ho_SymbolXLDs, out hv_Number);
                    if ((int)(new HTuple(hv_Number.TupleNotEqual(1))) != 0)
                    {
                        HOperatorSet.FindDataCode2d(ho_ImageScaled, out ho_SymbolXLDs, FormMain.QRCodeHandle,
                     new HTuple(), new HTuple(), out hv_ResultHandles, out hv_DecodedDataStrings);
                    }
                    HOperatorSet.CountObj(ho_SymbolXLDs, out hv_Number);
                    if ((int)(new HTuple(hv_Number.TupleNotEqual(1))) != 0)
                    {
                        hv_DecodedDataStrings = "000000";
                        logFile("FindCode fail", hv_Exception);
                        ho_ImageReduced.Dispose();
                        ho_ImageScaled.Dispose();

                        return;
                    }
                    get_CodeAngle(ho_SymbolXLDs, out hv_CodeAngle, out hv_CodeRow, out hv_CodeColumn);
                    if ((int)((new HTuple((new HTuple((new HTuple(hv_CodeRow.TupleLength())).TupleEqual(
                        0))).TupleOr(new HTuple((new HTuple(hv_CodeColumn.TupleLength())).TupleEqual(
                        0))))).TupleOr(new HTuple((new HTuple(hv_CodeAngle.TupleLength())).TupleEqual(
                        0)))) != 0)
                    {

                        hv_CodeAngle = 0;
                        hv_CodeRow = 0;
                        hv_CodeColumn = 0;

                        logFile("FindCode fail", hv_Exception);
                        ho_ImageReduced.Dispose();
                        ho_ImageScaled.Dispose();

                        return;
                    }

                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("FindCode fail", hv_Exception);
                }

                ho_ImageReduced.Dispose();
                ho_ImageScaled.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_ImageReduced.Dispose();
                ho_ImageScaled.Dispose();

                throw HDevExpDefaultException;
            }
        }

        // Short Description: 由Rectangle2获得Rectangle1的四个点 
        public void Get_Rectangle1_4Points(HObject ho_Region, out HTuple hv_Col1, out HTuple hv_Row1,
            out HTuple hv_Col2, out HTuple hv_Row2, out HTuple hv_Col3, out HTuple hv_Row3,
            out HTuple hv_Col4, out HTuple hv_Row4)
        {



            // Local iconic variables 

            // Local control variables 

            HTuple hv_aRow = new HTuple(), hv_aCol = new HTuple();
            HTuple hv_phi = new HTuple(), hv_lenth1 = new HTuple();
            HTuple hv_lenth2 = new HTuple(), hv_aa = new HTuple();
            HTuple hv_bb = new HTuple(), hv_diagonalLength = new HTuple();
            HTuple hv_angle = new HTuple(), hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            hv_Col1 = new HTuple();
            hv_Row1 = new HTuple();
            hv_Col2 = new HTuple();
            hv_Row2 = new HTuple();
            hv_Col3 = new HTuple();
            hv_Row3 = new HTuple();
            hv_Col4 = new HTuple();
            hv_Row4 = new HTuple();
            try
            {
                try
                {

                    HOperatorSet.SmallestRectangle2(ho_Region, out hv_aRow, out hv_aCol, out hv_phi,
                        out hv_lenth1, out hv_lenth2);

                    hv_Col1 = -1;

                    hv_Row1 = -1;

                    hv_Col2 = -1;

                    hv_Row2 = -1;

                    hv_Col3 = -1;

                    hv_Row3 = -1;

                    hv_Col4 = -1;

                    hv_Row4 = -1;

                    if ((int)(new HTuple(hv_lenth1.TupleEqual(0))) != 0)
                    {

                        return;
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_lenth1 = hv_lenth1 * 2;

                            hv_lenth1 = ExpTmpLocalVar_lenth1;
                        }
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_lenth2 = hv_lenth2 * 2;

                            hv_lenth2 = ExpTmpLocalVar_lenth2;
                        }
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_aa = (1.0 * hv_lenth2) / hv_lenth1;
                    }
                    //
                    if ((int)(new HTuple(hv_phi.TupleLess(0))) != 0)
                    {
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_phi = hv_phi + 3.14;

                                hv_phi = ExpTmpLocalVar_phi;
                            }
                        }
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_bb = (((1.0 * hv_lenth2) / hv_lenth1)).TupleAtan()
                            ;
                    }
                    //

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_diagonalLength = (((hv_lenth1 * hv_lenth1) + (hv_lenth2 * hv_lenth2))).TupleSqrt()
                            ;
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_angle = (((1.0 * hv_lenth2) / hv_lenth1)).TupleAtan()
                            ;
                    }
                    //

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Col1 = hv_aCol + ((hv_diagonalLength / 2.0) * (((hv_phi + hv_angle)).TupleCos()
                            ));
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Row1 = hv_aRow - ((hv_diagonalLength / 2.0) * (((hv_phi + hv_angle)).TupleSin()
                            ));
                    }
                    //

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Col2 = hv_aCol + ((hv_diagonalLength / 2.0) * (((hv_phi - hv_angle)).TupleCos()
                            ));
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Row2 = hv_aRow - ((hv_diagonalLength / 2.0) * (((hv_phi - hv_angle)).TupleSin()
                            ));
                    }
                    //

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Col3 = hv_aCol - ((hv_diagonalLength / 2.0) * (((hv_phi - hv_angle)).TupleCos()
                            ));
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Row3 = hv_aRow + ((hv_diagonalLength / 2.0) * (((hv_phi - hv_angle)).TupleSin()
                            ));
                    }
                    //

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Col4 = hv_aCol - ((hv_diagonalLength / 2.0) * (((hv_phi + hv_angle)).TupleCos()
                            ));
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Row4 = hv_aRow + ((hv_diagonalLength / 2.0) * (((hv_phi + hv_angle)).TupleSin()
                            ));
                    }


                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("Get_Rectangle1_4Points fail", hv_Exception);
                }



                return;
            }
            catch (HalconException HDevExpDefaultException)
            {

                throw HDevExpDefaultException;
            }
        }

        public void logFile(HTuple hv_Error, HTuple hv_Info)
        {



            // Local iconic variables 

            // Local control variables 

            HTuple hv_logPath = new HTuple(), hv_FileExists = new HTuple();
            HTuple hv_MSecond = new HTuple(), hv_Second = new HTuple();
            HTuple hv_Minute = new HTuple(), hv_Hour = new HTuple();
            HTuple hv_Day = new HTuple(), hv_YDay = new HTuple(), hv_Month = new HTuple();
            HTuple hv_Year = new HTuple(), hv_fileName = new HTuple();
            HTuple hv_filePath = new HTuple(), hv_format = new HTuple();
            HTuple hv_msg = new HTuple(), hv_Index = new HTuple();
            HTuple hv_FileHandle = new HTuple();
            // Initialize local and output iconic variables 
            try
            {

                hv_logPath = "../visionLog/";

                HOperatorSet.FileExists(hv_logPath, out hv_FileExists);
                if ((int)(new HTuple(hv_FileExists.TupleEqual(0))) != 0)
                {
                    HOperatorSet.MakeDir(hv_logPath);
                }

                HOperatorSet.GetSystemTime(out hv_MSecond, out hv_Second, out hv_Minute, out hv_Hour,
                    out hv_Day, out hv_YDay, out hv_Month, out hv_Year);

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_fileName = ((((hv_Year + "-") + hv_Month) + "-") + hv_Day) + ".txt";
                }

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_filePath = (hv_logPath + "/") + hv_fileName;
                }

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_format = ((((("[" + hv_Hour) + ":") + hv_Minute) + ":") + hv_Minute) + "]";
                }

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_msg = (hv_format + "") + hv_Error;
                }

                for (hv_Index = 0; (int)hv_Index <= (int)((new HTuple(hv_Info.TupleLength())) - 1); hv_Index = (int)hv_Index + 1)
                {
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_msg = (hv_msg + (hv_Info.TupleSelect(
                                hv_Index))) + " ";

                            hv_msg = ExpTmpLocalVar_msg;
                        }
                    }
                }

                HOperatorSet.OpenFile(hv_filePath, "append", out hv_FileHandle);
                HOperatorSet.FnewLine(hv_FileHandle);
                HOperatorSet.FwriteString(hv_FileHandle, hv_msg);
                HOperatorSet.CloseFile(hv_FileHandle);




                return;
            }
            catch (HalconException HDevExpDefaultException)
            {



                throw HDevExpDefaultException;
            }
        }

        // Local procedures 
        public void GetXLine(out HObject ho_Line, HTuple hv_xRow1, HTuple hv_xCol1, HTuple hv_xRow2,
            HTuple hv_xCol2, HTuple hv_cRow, HTuple hv_cCol, HTuple hv_xJd, out HTuple hv_lRow1,
            out HTuple hv_lCol1, out HTuple hv_lRow2, out HTuple hv_lCol2)
        {



            // Local iconic variables 

            // Local control variables 

            HTuple hv_Phi = new HTuple(), hv_Phi1 = new HTuple();
            HTuple hv_k = new HTuple(), hv_b = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Line);
            hv_lRow1 = new HTuple();
            hv_lCol1 = new HTuple();
            hv_lRow2 = new HTuple();
            hv_lCol2 = new HTuple();
            try
            {

                HOperatorSet.LineOrientation(hv_cRow, hv_cCol, hv_xRow2, hv_xCol2, out hv_Phi);


                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_Phi1 = hv_Phi + hv_xJd;
                }
                //Dist := cCol - (cRow * tan(Phi1)) *cos(Phi1)
                //if (Phi1>3.14)
                //Phi1 := 1.57-Phi1
                //endif


                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_k = hv_Phi1.TupleTan()
                        ;
                }

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_b = hv_cRow + (hv_cCol * hv_k);
                }

                //if (b>=0 and b<=5120)
                //row1 := b
                //col1 := 0
                //row2 := -5120*k+b
                //col2 := 5120
                //else
                //row1 := 0
                //col1 := b/k
                //row2 := 5120
                //col2 := -(5120-b)/k
                //endif

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_lRow1 = hv_cRow + (2000 * (hv_Phi1.TupleSin()
                        ));
                }

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_lCol1 = hv_cCol - (2000 * (hv_Phi1.TupleCos()
                        ));
                }

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_lRow2 = hv_cRow - (2000 * (hv_Phi1.TupleSin()
                        ));
                }

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_lCol2 = hv_cCol + (2000 * (hv_Phi1.TupleCos()
                        ));
                }
                //x := (5120-b)/Phi

                //gen_region_hline (Line, Phi1, Dist)
                //gen_region_line (RegionLines, 3949, 0, 3630, 5120)
                ho_Line.Dispose();
                HOperatorSet.GenRegionLine(out ho_Line, hv_lRow1, hv_lCol1, hv_lRow2, hv_lCol2);
                return;
            }
            catch (HalconException HDevExpDefaultException)
            {


                throw HDevExpDefaultException;
            }
        }

        public void Measure_X(HObject ho_Image2, out HObject ho_LineCC, out HObject ho_LineBB,
            out HObject ho_LineAA, out HObject ho_LineDD, HTuple hv_Rowbarcode, HTuple hv_Columnbarcode,
            HTuple hv_RowCentre, HTuple hv_ColCentre, out HTuple hv_LengthCC, out HTuple hv_LengthBB,
            out HTuple hv_LengthAA, out HTuple hv_LengthDD)
        {



            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_LineCC);
            HOperatorSet.GenEmptyObj(out ho_LineBB);
            HOperatorSet.GenEmptyObj(out ho_LineAA);
            HOperatorSet.GenEmptyObj(out ho_LineDD);
            hv_LengthCC = new HTuple();
            hv_LengthBB = new HTuple();
            hv_LengthAA = new HTuple();
            hv_LengthDD = new HTuple();



            return;
        }

        public void MeasuringLine(HObject ho_Image, out HObject ho_Rectangle, out HObject ho_Cross,
            out HObject ho_Cross1, HTuple hv_Row1, HTuple hv_Column1, HTuple hv_Row2, HTuple hv_Column2,
            out HTuple hv_Length)
        {



            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Rectangle);
            HOperatorSet.GenEmptyObj(out ho_Cross);
            HOperatorSet.GenEmptyObj(out ho_Cross1);
            hv_Length = new HTuple();

            //stop ()



            return;
        }

        public void MyDistance_PP(HObject ho_Circle, HObject ho_Line, HObject ho_Image2,
            out HObject ho_Edges, out HObject ho_Edges1, out HObject ho_Cross13, out HObject ho_Cross14,
            HTuple hv_WindowHandle, out HTuple hv_ROI_D, out HTuple hv_Row4, out HTuple hv_Column1,
            out HTuple hv_Row5, out HTuple hv_Column5, out HTuple hv_Distance)
        {




            // Local iconic variables 

            HObject ho_RegionBorder, ho_RegionIntersection;
            HObject ho_ConnectedRegions, ho_Circle1, ho_Circle2, ho_ImageReduced1;
            HObject ho_ImageReduced2, ho_ROI_0;

            // Local control variables 

            HTuple hv_Area = new HTuple(), hv_RowROIP = new HTuple();
            HTuple hv_ColumnROIP = new HTuple(), hv_Indices = new HTuple();
            HTuple hv_RowBegin = new HTuple(), hv_ColBegin = new HTuple();
            HTuple hv_RowEnd = new HTuple(), hv_ColEnd = new HTuple();
            HTuple hv_Nr = new HTuple(), hv_Nc = new HTuple(), hv_Dist = new HTuple();
            HTuple hv_IsParallel = new HTuple(), hv_RowBegin1 = new HTuple();
            HTuple hv_ColBegin1 = new HTuple(), hv_RowEnd1 = new HTuple();
            HTuple hv_ColEnd1 = new HTuple(), hv_Nr1 = new HTuple();
            HTuple hv_Nc1 = new HTuple(), hv_Dist1 = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Edges);
            HOperatorSet.GenEmptyObj(out ho_Edges1);
            HOperatorSet.GenEmptyObj(out ho_Cross13);
            HOperatorSet.GenEmptyObj(out ho_Cross14);
            HOperatorSet.GenEmptyObj(out ho_RegionBorder);
            HOperatorSet.GenEmptyObj(out ho_RegionIntersection);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_Circle1);
            HOperatorSet.GenEmptyObj(out ho_Circle2);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced1);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced2);
            HOperatorSet.GenEmptyObj(out ho_ROI_0);
            hv_ROI_D = new HTuple();
            hv_Row4 = new HTuple();
            hv_Column1 = new HTuple();
            hv_Row5 = new HTuple();
            hv_Column5 = new HTuple();
            hv_Distance = new HTuple();
            try
            {
                ho_RegionBorder.Dispose();
                HOperatorSet.Boundary(ho_Circle, out ho_RegionBorder, "inner");
                ho_RegionIntersection.Dispose();
                HOperatorSet.Intersection(ho_RegionBorder, ho_Line, out ho_RegionIntersection
                    );
                ho_ConnectedRegions.Dispose();
                HOperatorSet.Connection(ho_RegionIntersection, out ho_ConnectedRegions);

                HOperatorSet.AreaCenter(ho_ConnectedRegions, out hv_Area, out hv_RowROIP, out hv_ColumnROIP);
                //点1

                HOperatorSet.TupleSortIndex(hv_ColumnROIP, out hv_Indices);
                //选点ROI大小

                hv_ROI_D = 50;
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    ho_Circle1.Dispose();
                    HOperatorSet.GenCircle(out ho_Circle1, hv_RowROIP.TupleSelect(hv_Indices.TupleSelect(
                        0)), hv_ColumnROIP.TupleSelect(hv_Indices.TupleSelect(0)), hv_ROI_D);
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    ho_Circle2.Dispose();
                    HOperatorSet.GenCircle(out ho_Circle2, hv_RowROIP.TupleSelect(hv_Indices.TupleSelect(
                        1)), hv_ColumnROIP.TupleSelect(hv_Indices.TupleSelect(1)), hv_ROI_D);
                }
                ho_ImageReduced1.Dispose();
                HOperatorSet.ReduceDomain(ho_Image2, ho_Circle1, out ho_ImageReduced1);
                ho_ImageReduced2.Dispose();
                HOperatorSet.ReduceDomain(ho_Image2, ho_Circle2, out ho_ImageReduced2);


                ho_ROI_0.Dispose();
                HOperatorSet.GenCircle(out ho_ROI_0, 3041.12, 4797.74, 99.6921);
                ho_ImageReduced1.Dispose();
                HOperatorSet.ReduceDomain(ho_Image2, ho_ROI_0, out ho_ImageReduced1);

                //拟合点位ROI直线   然后与米字线求交点
                ho_Edges.Dispose();
                HOperatorSet.EdgesSubPix(ho_ImageReduced1, out ho_Edges, "canny", 1, 20, 40);

                HOperatorSet.FitLineContourXld(ho_Edges, "tukey", -1, 0, 5, 2, out hv_RowBegin,
                    out hv_ColBegin, out hv_RowEnd, out hv_ColEnd, out hv_Nr, out hv_Nc, out hv_Dist);
                if (HDevWindowStack.IsOpen())
                {
                    HOperatorSet.SetColor(HDevWindowStack.GetActive(), "green");
                }
                HOperatorSet.DispLine(hv_WindowHandle, hv_RowBegin, hv_ColBegin, hv_RowEnd,
                    hv_ColEnd);
                //交点就是找的点了
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {

                    HOperatorSet.IntersectionLl(hv_RowBegin, hv_ColBegin, hv_RowEnd, hv_ColEnd,
                        hv_RowROIP.TupleSelect(hv_Indices.TupleSelect(0)), hv_ColumnROIP.TupleSelect(
                        hv_Indices.TupleSelect(0)), hv_RowROIP.TupleSelect(hv_Indices.TupleSelect(
                        1)), hv_ColumnROIP.TupleSelect(hv_Indices.TupleSelect(1)), out hv_Row4,
                        out hv_Column1, out hv_IsParallel);
                }
                ho_Cross13.Dispose();
                HOperatorSet.GenCrossContourXld(out ho_Cross13, hv_Row4, hv_Column1, 30, 0.7);
                ho_Edges1.Dispose();
                HOperatorSet.EdgesSubPix(ho_ImageReduced2, out ho_Edges1, "canny", 1, 20, 40);

                HOperatorSet.FitLineContourXld(ho_Edges1, "tukey", -1, 0, 5, 2, out hv_RowBegin1,
                    out hv_ColBegin1, out hv_RowEnd1, out hv_ColEnd1, out hv_Nr1, out hv_Nc1,
                    out hv_Dist1);

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {

                    HOperatorSet.IntersectionLl(hv_RowBegin1, hv_ColBegin1, hv_RowEnd1, hv_ColEnd1,
                        hv_RowROIP.TupleSelect(hv_Indices.TupleSelect(0)), hv_ColumnROIP.TupleSelect(
                        hv_Indices.TupleSelect(0)), hv_RowROIP.TupleSelect(hv_Indices.TupleSelect(
                        1)), hv_ColumnROIP.TupleSelect(hv_Indices.TupleSelect(1)), out hv_Row5,
                        out hv_Column5, out hv_IsParallel);
                }
                ho_Cross14.Dispose();
                HOperatorSet.GenCrossContourXld(out ho_Cross14, hv_Row5, hv_Column5, 30, 0.7);
                //求距离

                HOperatorSet.DistancePp(hv_Row4, hv_Column1, hv_Row5, hv_Column5, out hv_Distance);
                ho_RegionBorder.Dispose();
                ho_RegionIntersection.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_Circle1.Dispose();
                ho_Circle2.Dispose();
                ho_ImageReduced1.Dispose();
                ho_ImageReduced2.Dispose();
                ho_ROI_0.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_RegionBorder.Dispose();
                ho_RegionIntersection.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_Circle1.Dispose();
                ho_Circle2.Dispose();
                ho_ImageReduced1.Dispose();
                ho_ImageReduced2.Dispose();
                ho_ROI_0.Dispose();

                throw HDevExpDefaultException;
            }
        }
        #endregion

        #region LOAD算法

        public void CreateloadCapResultImage(HObject ho_SrcImage, HObject ho_ShowRegion,
        out HObject ho_ResultImage, HTuple hv_CodeRow, HTuple hv_CodeCol, HTuple hv_Row,
        HTuple hv_Col, HTuple hv_Angle)
        {




            // Local iconic variables 

            // Local control variables 

            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_WindowHandle = new HTuple(), hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ResultImage);
            try
            {
                ho_ResultImage.Dispose();
                ho_ResultImage = ho_SrcImage.CopyObj(1, -1);

                HOperatorSet.GetImageSize(ho_SrcImage, out hv_Width, out hv_Height);
                HOperatorSet.OpenWindow(0, 0, hv_Width / 4, hv_Height / 4, 0, "invisible", "", out hv_WindowHandle);

                HOperatorSet.SetLineWidth(hv_WindowHandle, 10);
                HOperatorSet.SetColor(hv_WindowHandle, "green");
                HOperatorSet.SetFont(hv_WindowHandle, "-Times New Roman-100-");

                HOperatorSet.DispObj(ho_ResultImage, hv_WindowHandle);
                HOperatorSet.SetDraw(hv_WindowHandle, "fill");
                if ((int)(new HTuple((new HTuple(hv_Row.TupleLength())).TupleEqual(0))) != 0)
                {
                    HOperatorSet.SetColor(hv_WindowHandle, "yellow");
                    HOperatorSet.SetTposition(hv_WindowHandle, 0.5 * hv_Height, 0.5 * hv_Width);
                    HOperatorSet.WriteString(hv_WindowHandle, "---Abnomal！---");
                    ho_ResultImage.Dispose();
                    HOperatorSet.DumpWindowImage(out ho_ResultImage, hv_WindowHandle);
                    HOperatorSet.CloseWindow(hv_WindowHandle);

                    return;
                }


                HOperatorSet.DispObj(ho_ShowRegion, hv_WindowHandle);
                HOperatorSet.SetTposition(hv_WindowHandle, 0.9 * hv_Height, 0.1 * hv_Width);
                HOperatorSet.WriteString(hv_WindowHandle, (((("Row: " + hv_Row) + "  Col:") + hv_Col) + "  Angle:") + hv_Angle);



                ho_ResultImage.Dispose();
                HOperatorSet.DumpWindowImage(out ho_ResultImage, hv_WindowHandle);

                //write_image (ResultImage, 'bmp', 0, 'D:/bomming/CYG610-V2.0/Load.bmp')
                HOperatorSet.CloseWindow(hv_WindowHandle);
            }
            // catch (Exception) 
            catch (HalconException HDevExpDefaultException1)
            {
                HDevExpDefaultException1.ToHTuple(out hv_Exception);
                logFile("CreateloadCapResultImage fail", hv_Exception);
            }


            return;
        }

        // Short Description: 找圆心 
        public void GetCircleCenter(HObject ho_SrcImage, out HObject ho_Circle, HTuple hv_GrayMin,
            out HTuple hv_Row, out HTuple hv_Column, out HTuple hv_Radius)
        {




            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_Contours, ho_Region1 = null, ho_ConnectedRegions1 = null;
            HObject ho_SelectedRegions = null, ho_RegionUnion = null, ho_RegionOpening = null;

            // Local control variables 

            HTuple hv_Number = new HTuple(), hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Circle);
            HOperatorSet.GenEmptyObj(out ho_Contours);
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions1);
            HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
            HOperatorSet.GenEmptyObj(out ho_RegionUnion);
            HOperatorSet.GenEmptyObj(out ho_RegionOpening);
            try
            {
                ho_Contours.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Contours);
                ho_Circle.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Circle);
                hv_Row = -1;
                hv_Column = -1;
                hv_Radius = -1;

                try
                {

                    ho_Region1.Dispose();
                    HOperatorSet.Threshold(ho_SrcImage, out ho_Region1, hv_GrayMin, 255);

                    ho_ConnectedRegions1.Dispose();
                    HOperatorSet.Connection(ho_Region1, out ho_ConnectedRegions1);
                    ho_SelectedRegions.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions1, out ho_SelectedRegions,
                        "max_area", 70);
                    ho_RegionUnion.Dispose();
                    HOperatorSet.Union1(ho_SelectedRegions, out ho_RegionUnion);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.FillUp(ho_RegionUnion, out ExpTmpOutVar_0);
                        ho_RegionUnion.Dispose();
                        ho_RegionUnion = ExpTmpOutVar_0;
                    }
                    //
                    ho_RegionOpening.Dispose();
                    HOperatorSet.OpeningCircle(ho_RegionUnion, out ho_RegionOpening, 450);
                    HOperatorSet.CountObj(ho_RegionOpening, out hv_Number);
                    if ((int)(new HTuple(hv_Number.TupleEqual(0))) != 0)
                    {
                        ho_Contours.Dispose();
                        ho_Region1.Dispose();
                        ho_ConnectedRegions1.Dispose();
                        ho_SelectedRegions.Dispose();
                        ho_RegionUnion.Dispose();
                        ho_RegionOpening.Dispose();

                        return;
                    }
                    HOperatorSet.SmallestCircle(ho_RegionOpening, out hv_Row, out hv_Column,
                        out hv_Radius);

                    ho_Circle.Dispose();
                    HOperatorSet.GenCircleContourXld(out ho_Circle, hv_Row, hv_Column, hv_Radius,
                        0, 6.28318, "positive", 1);

                    //
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("GetCircleCentre fail", hv_Exception);
                }
                ho_Contours.Dispose();
                ho_Region1.Dispose();
                ho_ConnectedRegions1.Dispose();
                ho_SelectedRegions.Dispose();
                ho_RegionUnion.Dispose();
                ho_RegionOpening.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Contours.Dispose();
                ho_Region1.Dispose();
                ho_ConnectedRegions1.Dispose();
                ho_SelectedRegions.Dispose();
                ho_RegionUnion.Dispose();
                ho_RegionOpening.Dispose();

                throw HDevExpDefaultException;
            }
        }

        public void Get2PointAngle(HTuple hv_X1, HTuple hv_Y1, HTuple hv_X2, HTuple hv_Y2,
        out HTuple hv_Angle)
        {

            // Local iconic variables 

            // Local control variables 

            HTuple hv_x = null, hv_y = null, hv_Powx = null;
            HTuple hv_Powy = null, hv_hypotenuse = null, hv_icos = null;
            HTuple hv_radian = null, hv_iangle = null;
            // Initialize local and output iconic variables 
            hv_x = hv_X2 - hv_X1;
            hv_y = hv_Y2 - hv_Y1;
            HOperatorSet.TuplePow(hv_x, 2, out hv_Powx);
            HOperatorSet.TuplePow(hv_y, 2, out hv_Powy);

            HOperatorSet.TupleSqrt(hv_Powy + hv_Powx, out hv_hypotenuse);
            hv_icos = hv_x / hv_hypotenuse;
            HOperatorSet.TupleAcos(hv_icos, out hv_radian);
            hv_iangle = 180 / (3.1415926 / hv_radian);
            if ((int)(new HTuple(hv_y.TupleGreater(0))) != 0)
            {
                hv_iangle = -hv_iangle;
            }
            else if ((int)((new HTuple(hv_y.TupleEqual(0))).TupleAnd(new HTuple(hv_x.TupleLess(
                0)))) != 0)
            {
                hv_iangle = 180;
            }
            hv_Angle = hv_iangle.Clone();

            return;
        }

        public void LocationCap(HObject ho_SrcImage, HObject ho_LoadROIs, out HObject ho_ResultImage,
        HTuple hv_DataCodeHandle, HTuple hv_MarkAngle, HTuple hv_Method, HTuple hv_GrayMin,
        out HTuple hv_DecodedDataStrings, out HTuple hv_RowCentre, out HTuple hv_ColumnCentre,
        out HTuple hv_Radius, out HTuple hv_CodeRow, out HTuple hv_CodeColumn, out HTuple hv_Row,
        out HTuple hv_Col, out HTuple hv_Angle)
        {




            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_Circle = null, ho_CodeRoi = null, ho_MarkRoi = null;
            HObject ho_EmptyObject = null, ho_SymbolXLDs = null, ho_MarkRoiAffineTrans = null;
            HObject ho_Region = null, ho_ShowRegion = null, ho_Arrow = null;

            // Local control variables 

            HTuple hv_PI = new HTuple(), hv_Number = new HTuple();
            HTuple hv_Area = new HTuple(), hv_Column = new HTuple();
            HTuple hv_Indices = new HTuple(), hv_CodeAngle = new HTuple();
            HTuple hv_HomMat2D = new HTuple(), hv_Col1 = new HTuple();
            HTuple hv_Row1 = new HTuple(), hv_Col2 = new HTuple();
            HTuple hv_Row2 = new HTuple(), hv_Col3 = new HTuple();
            HTuple hv_Row3 = new HTuple(), hv_Col4 = new HTuple();
            HTuple hv_Row4 = new HTuple(), hv_CapAngle = new HTuple();
            HTuple hv_Exception = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ResultImage);
            HOperatorSet.GenEmptyObj(out ho_Circle);
            HOperatorSet.GenEmptyObj(out ho_CodeRoi);
            HOperatorSet.GenEmptyObj(out ho_MarkRoi);
            HOperatorSet.GenEmptyObj(out ho_EmptyObject);
            HOperatorSet.GenEmptyObj(out ho_SymbolXLDs);
            HOperatorSet.GenEmptyObj(out ho_MarkRoiAffineTrans);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_ShowRegion);
            HOperatorSet.GenEmptyObj(out ho_Arrow);
            hv_DecodedDataStrings = new HTuple();
            hv_RowCentre = new HTuple();
            hv_ColumnCentre = new HTuple();
            hv_Radius = new HTuple();
            hv_CodeRow = new HTuple();
            hv_CodeColumn = new HTuple();
            hv_Row = new HTuple();
            hv_Col = new HTuple();
            hv_Angle = new HTuple();
            try
            {
                try
                {
                    hv_RowCentre = -1;
                    hv_ColumnCentre = -1;
                    hv_Radius = -1;
                    hv_PI = 3.1415926;
                    //ÕÒÔ²ÐÄ    *ÉÏÁÏµÄÕâÀïÐèÒªÖØÐÂÐ´
                    ho_Circle.Dispose();
                    GetCircleCenter(ho_SrcImage, out ho_Circle, hv_GrayMin, out hv_RowCentre,
                        out hv_ColumnCentre, out hv_Radius);
                    HOperatorSet.CountObj(ho_LoadROIs, out hv_Number);
                    HOperatorSet.AreaCenter(ho_LoadROIs, out hv_Area, out hv_Row, out hv_Column);
                    HOperatorSet.TupleSortIndex(hv_Area, out hv_Indices);
                    ho_CodeRoi.Dispose();
                    HOperatorSet.SelectObj(ho_LoadROIs, out ho_CodeRoi, (hv_Indices.TupleSelect(
                        (new HTuple(hv_Indices.TupleLength())) - 1)) + 1);
                    ho_MarkRoi.Dispose();
                    HOperatorSet.SelectObj(ho_LoadROIs, out ho_MarkRoi, (hv_Indices.TupleSelect(
                        (new HTuple(hv_Indices.TupleLength())) - 2)) + 1);

                    //ÕÒ¶þÎ¬Âë
                    ho_EmptyObject.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_EmptyObject);
                    ho_SymbolXLDs.Dispose();
                    FindCode(ho_SrcImage, ho_EmptyObject, out ho_SymbolXLDs, hv_DataCodeHandle,
                        out hv_DecodedDataStrings, out hv_CodeRow, out hv_CodeColumn, out hv_CodeAngle);
                    if ((int)((new HTuple((new HTuple(hv_CodeRow.TupleEqual(-1))).TupleOr(new HTuple(hv_CodeColumn.TupleEqual(
                        -1))))).TupleOr(new HTuple(hv_CodeAngle.TupleEqual(-100)))) != 0)
                    {
                        ho_Circle.Dispose();
                        ho_CodeRoi.Dispose();
                        ho_MarkRoi.Dispose();
                        ho_EmptyObject.Dispose();
                        ho_SymbolXLDs.Dispose();
                        ho_MarkRoiAffineTrans.Dispose();
                        ho_Region.Dispose();
                        ho_ShowRegion.Dispose();
                        ho_Arrow.Dispose();

                        return;
                    }

                    if ((int)(new HTuple(hv_Method.TupleEqual("Code+OneMark"))) != 0)
                    {
                        //¾ÀÆ«Mark Roi
                        HOperatorSet.VectorAngleToRigid(hv_CodeRow, hv_CodeColumn, hv_CodeAngle,
                            hv_CodeRow, hv_CodeColumn, hv_CodeAngle, out hv_HomMat2D);
                        ho_MarkRoiAffineTrans.Dispose();
                        HOperatorSet.AffineTransRegion(ho_MarkRoi, out ho_MarkRoiAffineTrans, hv_HomMat2D,
                            "nearest_neighbor");

                        //2¡¢ÕÒMarkµã
                        ho_Region.Dispose();
                        Find_Mark_Region(ho_SrcImage, ho_MarkRoiAffineTrans, out ho_Region);
                        Get_Rectangle1_4Points(ho_Region, out hv_Col1, out hv_Row1, out hv_Col2,
                            out hv_Row2, out hv_Col3, out hv_Row3, out hv_Col4, out hv_Row4);
                        hv_Row = hv_Row1.Clone();
                        hv_Col = hv_Col1.Clone();
                    }
                    else if ((int)(new HTuple(hv_Method.TupleEqual("Code+Centre"))) != 0)
                    {
                        //
                        HOperatorSet.LineOrientation(hv_RowCentre, hv_ColumnCentre, hv_CodeRow,
                            hv_CodeColumn, out hv_CapAngle);
                        hv_Row = hv_RowCentre.Clone();
                        hv_Col = hv_ColumnCentre.Clone();
                    }
                    ho_ShowRegion.Dispose();
                    ho_ShowRegion = ho_SymbolXLDs.CopyObj(1, -1);
                    ho_Arrow.Dispose();
                    gen_arrow_contour_xld(out ho_Arrow, hv_Row, hv_Col, hv_CodeRow, hv_CodeColumn,
                        100, 50);



                    ho_ShowRegion.Dispose();
                    HOperatorSet.GenRegionContourXld(ho_SymbolXLDs, out ho_ShowRegion, "margin");

                    ho_ShowRegion.Dispose();
                    HOperatorSet.ConcatObj(ho_Arrow, ho_SymbolXLDs, out ho_ShowRegion);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_ShowRegion, ho_Circle, out ExpTmpOutVar_0);
                        ho_ShowRegion.Dispose();
                        ho_ShowRegion = ExpTmpOutVar_0;
                    }

                    Get2PointAngle(hv_Col, hv_Row, hv_CodeColumn, hv_CodeRow, out hv_Angle);
                    hv_Angle = (hv_Angle * 0.0055555) * 3.14;
                    //Angle := CapAngle*180/3.1415926
                    ho_ResultImage.Dispose();
                    ho_ResultImage = ho_SrcImage.Clone();
                    if (FormProdctPar.checkBox_show_result_image_cam0.Checked)
                    {
                        CreateloadCapResultImage(ho_SrcImage, ho_ShowRegion, out ho_ResultImage,
                       hv_CodeRow, hv_CodeColumn, hv_Row, hv_Col, hv_Angle);

                    }

                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    logFile("createLocationResultImage fail", hv_Exception);
                }

                ho_Circle.Dispose();
                ho_CodeRoi.Dispose();
                ho_MarkRoi.Dispose();
                ho_EmptyObject.Dispose();
                ho_SymbolXLDs.Dispose();
                ho_MarkRoiAffineTrans.Dispose();
                ho_Region.Dispose();
                ho_ShowRegion.Dispose();
                ho_Arrow.Dispose();

                return;


            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Circle.Dispose();
                ho_CodeRoi.Dispose();
                ho_MarkRoi.Dispose();
                ho_EmptyObject.Dispose();
                ho_SymbolXLDs.Dispose();
                ho_MarkRoiAffineTrans.Dispose();
                ho_Region.Dispose();
                ho_ShowRegion.Dispose();
                ho_Arrow.Dispose();

                throw HDevExpDefaultException;
            }
        }

        // Short Description: 读取Cap产品参数 
        public void ReadCapParams(out HObject ho_ModelImage, HTuple hv_ProductName, out HTuple hv_CalibData,
            out HTuple hv_CodeHandle)
        {
            try
            {

                string strProductName = string.Empty;
                HTuple hv_ItemPath = null;
                strProductName = comboBox_select_product.SelectedItem.ToString();
                if (comboBox_select_product.SelectedItem.ToString() != string.Empty)
                {
                    if (!Directory.Exists("product"))
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo("product");
                        directoryInfo.Create();
                    }
                    if (!Directory.Exists("product/" + strProductName))
                    {
                        DirectoryInfo directoryInfo1 = new DirectoryInfo("product/" + strProductName);
                        directoryInfo1.Create();
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_ItemPath = ("product/" + strProductName) + "/";
                    }
                }

                // Local iconic variables 

                // Local control variables 

                HTuple hv_Path = new HTuple(), hv_FileExists = new HTuple();
                HTuple hv_ProductPath = new HTuple();
                // Initialize local and output iconic variables 
                HOperatorSet.GenEmptyObj(out ho_ModelImage);
                hv_CalibData = new HTuple();
                hv_CodeHandle = new HTuple();

                //*产品配置项

                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    ho_ModelImage.Dispose();
                    HOperatorSet.ReadImage(out ho_ModelImage, hv_ItemPath + "modelImage.bmp");
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {

                    HOperatorSet.ReadTuple(hv_ItemPath + "calibData.tup", out hv_CalibData);
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {

                    HOperatorSet.ReadTuple(hv_ItemPath + "CodeHandle.tup", out hv_CodeHandle);
                }


                return;
            }
            catch (HalconException HDevExpDefaultException)
            {


                throw HDevExpDefaultException;
            }
        }

        // Short Description: 写入产品配置参数 
        public void WriteCapModelParams(HObject ho_modelImage, HTuple hv_ProductName, HTuple hv_tuple,
            HTuple hv_CodeHandle)
        {
            try
            {

                string strProductName = string.Empty;
                HTuple hv_ItemPath = null;
                strProductName = comboBox_select_product.SelectedItem.ToString();
                if (comboBox_select_product.SelectedItem.ToString() != string.Empty)
                {
                    if (!Directory.Exists("product"))
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo("product");
                        directoryInfo.Create();
                    }
                    if (!Directory.Exists("product/" + strProductName))
                    {
                        DirectoryInfo directoryInfo1 = new DirectoryInfo("product/" + strProductName);
                        directoryInfo1.Create();
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_ItemPath = ("product/" + strProductName) + "/";
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.WriteImage(ho_modelImage, "bmp", 0, hv_ItemPath + "modelImage.bmp");
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.WriteTuple(hv_tuple, hv_ItemPath + "value.tup");
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.WriteTuple(hv_CodeHandle, hv_ItemPath + "CodeHandle.tup");
                    }
                    return;
                }
            }
            catch (HalconException HDevExpDefaultException)
            {

                throw HDevExpDefaultException;
            }
        }



    }
}
#endregion





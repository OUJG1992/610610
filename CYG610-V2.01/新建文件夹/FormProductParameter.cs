using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using HWindow;
using HalconDotNet;
using System.IO;
using csLTDMC;


namespace upperComputer
{
    public partial class FormProductParameter : Form
    {
        public delegate void ProductDlgSendToMainDlg(string message, object data);
        public static event ProductDlgSendToMainDlg productDlgSendToMainDlg;

        HTuple hv_ProductName;

        public SortedList<string, Roi> cam0Roi { set; get; }
        public SortedList<string, HWin.RoiCell> cam1Roi { set; get; }
        int Cam0LocRoiX1, Cam0LocRoiX2, Cam0LocRoiY1, Cam0LocRoiY2;     //ROI点位
        int Cam1LocRoiX1, Cam1LocRoiX2, Cam1LocRoiY1, Cam1LocRoiY2;

        private Detector TestDetector;
        private HObject SrcImg;

        public bool hWin1_UpdataFromIni = false;

        private void FormProductParameter_Load(object sender, EventArgs e)
        {
            comboBox_select_product.Enabled = false;

            btn_OnOffParam.Text = "参数编辑开关-- > 开";
            tabPage_cam0_par.Parent = null;
            tabPage_cam1_par.Parent = null;


        }

        public static FormProductParameter Instance;
        public FormProductParameter()
        {
            Instance = this;

            cam0Roi = new SortedList<string, Roi>();
            cam1Roi = new SortedList<string, HWin.RoiCell>();

            InitializeComponent();
            TestDetector = new Detector();
            FormProductParameter_Load(this, null);

            loadProductToComboBox();
            txt_Department.Text ="0" ;

            FormMain.mainDlgSendToProductDlg += new FormMain.MainDlgSendToProductDlg(ProcessMainDlgMsg);

            hv_ProductName = new HTuple();

            hWin0.RegisterModifyRoiCallBack(new HWin.ModifyRoiCallBack(hWin0_ModifyRoi));
            hWin0.SetRoiEvent += new HWin.SetRoiDelegate(hWin0_SetRoi);

            hWin1.RegisterModifyRoiCallBack(new HWin.ModifyRoiCallBack(hWin1_ModifyRoi));
            hWin1.SetRoiEvent += new HWin.SetRoiDelegate(hWin1_SetRoi);

            InitRoi();
            InitVisionParam();

            #region VisionInitial

            // Detectors.readParams(Detectors.progName);
        }

        private void InitRoi()
        {
            string strProductName = string.Empty;
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
                //cam0 参数
                if (!File.Exists("product/" + strProductName + "/cam0par.ini"))
                {
                    FileStream fs = File.Create("product/" + strProductName + "/cam0par.ini");
                    fs.Close();
                }
                string IniPath = string.Empty;
                IniPath = "product/" + strProductName + "/cam0par.ini";

                InIClass Inicam0par = new InIClass(IniPath);
                int RoiNum = Convert.ToInt32(Inicam0par.Read("product", "roi_num"));

                hWin0_Roilist.Clear();
                comboBox_cam0_roi.Items.Clear();
                for (int i = 1; i < RoiNum + 1; i++)
                {
                    string iniSection = "Load-" + i.ToString();
                    HWin.RoiCell TempRoiCell = new HWin.RoiCell();
                    HWin.Roi TempRectPix = new HWin.Roi();
                    TempRoiCell.RoiName = iniSection;
                    TempRectPix.RoiType = Convert.ToInt32(Inicam0par.Read(iniSection, "RoiType"));
                    TempRectPix.Y1 = Convert.ToInt32(Inicam0par.Read(iniSection, "y1"));
                    TempRectPix.X1 = Convert.ToInt32(Inicam0par.Read(iniSection, "x1"));
                    TempRectPix.Y2 = Convert.ToInt32(Inicam0par.Read(iniSection, "y2"));
                    TempRectPix.X2 = Convert.ToInt32(Inicam0par.Read(iniSection, "x2"));
                    TempRoiCell.RoiPix = TempRectPix;
                    hWin0_Roilist.Add(TempRoiCell.RoiName, TempRoiCell);
                    comboBox_cam0_roi.Items.Add(iniSection);
                }
                comboBox_cam0_roi.SelectedIndex = 0;

                //cam1 参数
                if (!File.Exists("product/" + strProductName + "/cam1par.ini"))
                {
                    FileStream fs = File.Create("product/" + strProductName + "/cam1par.ini");
                    fs.Close();
                }
                IniPath = string.Empty;
                IniPath = "product/" + strProductName + "/cam1par.ini";

                InIClass Inicam1par = new InIClass(IniPath);
                RoiNum = Convert.ToInt32(Inicam1par.Read("product", "roi_num"));

                hWin1_Roilist.Clear();
                comboBox_cam1_roi.Items.Clear();
                for (int i = 1; i < RoiNum + 1; i++)
                {
                    string iniSection = "Mark" + i.ToString();
                    HWin.RoiCell TempRoiCell = new HWin.RoiCell();
                    HWin.Roi TempRectPix = new HWin.Roi();
                    TempRoiCell.RoiName = iniSection;
                    TempRectPix.RoiType = Convert.ToInt32(Inicam1par.Read(iniSection, "RoiType"));
                    TempRectPix.Y1 = Convert.ToInt32(Inicam1par.Read(iniSection, "y1"));
                    TempRectPix.X1 = Convert.ToInt32(Inicam1par.Read(iniSection, "x1"));
                    TempRectPix.Y2 = Convert.ToInt32(Inicam1par.Read(iniSection, "y2"));
                    TempRectPix.X2 = Convert.ToInt32(Inicam1par.Read(iniSection, "x2"));
                    TempRoiCell.RoiPix = TempRectPix;
                    hWin1_Roilist.Add(TempRoiCell.RoiName, TempRoiCell);
                    comboBox_cam1_roi.Items.Add(iniSection);
                }
                comboBox_cam1_roi.SelectedIndex = 0;


            }
        }
        private void InitVisionParam()
        {
            string strProductName = string.Empty;
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
                //cam0 参数
                if (!File.Exists("product/" + strProductName + "/cam0par.ini"))
                {
                    FileStream fs = File.Create("product/" + strProductName + "/cam0par.ini");
                    fs.Close();
                }
                string IniPath = string.Empty;
                IniPath = "product/" + strProductName + "/cam0par.ini";

                InIClass Inicam0par = new InIClass(IniPath);
                UpDownCam0_Pix2MM.Value = Convert.ToDecimal(Inicam0par.Read("Vision", "Pix2MM"));
                UpDown_Cam0_ExposureTime.Value = Convert.ToDecimal(Inicam0par.Read("Vision", "Exposure0"));
                combCodeType.Text = Inicam0par.Read("Vision", "CodeType");
                numericUD_Cam0GrayMin.Value = Convert.ToDecimal(Inicam0par.Read("Vision", "GrayMin"));
                CombLocMethod.Text = Inicam0par.Read("Vision", "LocMethod");            

               

                //*cam0 标准放料位
                Std_AX1.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-1X"));
                Std_AY1.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-1Y"));
                Std_Aangle1.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-1angle"));

                Std_AX2.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-2X"));
                Std_AY2.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-2Y"));
                Std_Aangle2.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-2angle"));


                Std_AX3.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-3X"));
                Std_AY3.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-3Y"));
                Std_Aangle3.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdA-3angle"));

                Std_BX1.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-1X"));
                Std_BY1.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-1Y"));
                Std_Bangle1.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-1angle"));

                Std_BX2.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-2X"));
                Std_BY2.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-2Y"));
                Std_Bangle2.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-2angle"));

                Std_BX3.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-3X"));
                Std_BY3.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-3Y"));
                Std_Bangle3.Value = Convert.ToDecimal(Inicam0par.Read("Load", "StdB-3angle"));

                //*cam0 6吸嘴旋转中心
                numUD_1RotateR.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "1RotateR"));
                numUD_1RotateC.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "1RotateC"));
                numUD_2RotateR.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "2RotateR"));
                numUD_2RotateC.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "2RotateC"));
                numUD_3RotateR.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "3RotateR"));
                numUD_3RotateC.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "3RotateC"));
                numUD_4RotateR.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "4RotateR"));
                numUD_4RotateC.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "4RotateC"));
                numUD_5RotateR.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "5RotateR"));
                numUD_5RotateC.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "5RotateC"));
                numUD_6RotateR.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "6RotateR"));
                numUD_6RotateC.Value = Convert.ToDecimal(Inicam0par.Read("RotateRC", "6RotateC"));

                //cam1 参数
                if (!File.Exists("product/" + strProductName + "/cam1par.ini"))
                {
                    FileStream fs = File.Create("product/" + strProductName + "/cam1par.ini");
                    fs.Close();
                }
                IniPath = string.Empty;
                IniPath = "product/" + strProductName + "/cam1par.ini";
                InIClass Inicam1par = new InIClass(IniPath);
                UpDownCam1_Pix2MM.Value = Convert.ToDecimal(Inicam1par.Read("Vision", "Pix2MM"));
                UpDown_Cam1_ExposureTime.Value = Convert.ToDecimal(Inicam1par.Read("Vision", "Exposure1"));
                UpDown_Cam1_CoordAngleOffset.Value= Convert.ToDecimal(Inicam1par.Read("Vision", "CoordAngleOffset"));

                UpDown_DiaStd.Value = Convert.ToDecimal(Inicam1par.Read("TechParam", "Dstd"));
                UpDown_upperDeviation.Value = Convert.ToDecimal(Inicam1par.Read("TechParam", "upperDeviation"));
                UpDown_lowerDeviation.Value = Convert.ToDecimal(Inicam1par.Read("TechParam", "lowerDeviation"));

                NDU_AAoff.Value = Convert.ToDecimal(Inicam1par.Read("OFFSET", "AAoffset"));
                NDU_BBoff.Value = Convert.ToDecimal(Inicam1par.Read("OFFSET", "BBoffset"));
                NDU_CCoff.Value = Convert.ToDecimal(Inicam1par.Read("OFFSET", "CCoffset"));
                NDU_DDoff.Value = Convert.ToDecimal(Inicam1par.Read("OFFSET", "DDoffset"));

                cmb_MultiLine.Text = Inicam1par.Read("GRR", "MultiLine");
                numericUpDown_MultiAngle.Text = Inicam1par.Read("GRR", "MultiAngle");

                //其他 参数
                if (!File.Exists("product/" + strProductName + "/other.ini"))
                {
                    FileStream fs = File.Create("product/" + strProductName + "/other.ini");
                    fs.Close();
                }
                IniPath = string.Empty;
                IniPath = "product/" + strProductName + "/other.ini";
                InIClass Iniotherpar = new InIClass(IniPath);
                txt_Department .Text = Iniotherpar.Read("other", "DepartmentCode");
                textBox_MachineID.Text = Iniotherpar.Read("other", "MachineCode");
                textBox_Batch.Text = Iniotherpar.Read("other", "Batch");

               
                           
            }
        }
        //窗口控件回调
        private SortedList<string, HWin.RoiCell> hWin0_Roilist = new SortedList<string, HWin.RoiCell>();
        private void hWin0_ModifyRoi(string RoiName, HWin.Roi RoiPix)
        {
            try
            {
                HWin.RoiCell TempRoiCell = new HWin.RoiCell();
                hWin0_Roilist = hWin0.RoiCellList;

                TempRoiCell.RoiName = RoiName;
                TempRoiCell.RoiPix = RoiPix;
                hWin0_Roilist.Add(RoiName, TempRoiCell);

                //修改Roi           
                Roi TempRoi = new Roi();

                TempRoi.RoiName = TempRoiCell.RoiName;
                TempRoi.RoiType = TempRoiCell.RoiPix.RoiType;
                Cam0LocRoiY1 = TempRoi.Row1 = TempRoiCell.RoiPix.Y1;
                Cam0LocRoiX1 = TempRoi.Column1 = TempRoiCell.RoiPix.X1;
                Cam0LocRoiY2 = TempRoi.Row2 = TempRoiCell.RoiPix.Y2;
                Cam0LocRoiX2 = TempRoi.Column2 = TempRoiCell.RoiPix.X2;

                hWin0.DisPlay(hWin0.HObj.Clone(), null, string.Empty, true, true, false);
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
        private void hWin0_SetRoi()
        {
            hWin0.RoiCellList = hWin0_Roilist;
        }

        private SortedList<string, HWin.RoiCell> hWin1_Roilist = new SortedList<string, HWin.RoiCell>();
        private void hWin1_ModifyRoi(string RoiName, HWin.Roi RoiPix)
        {
            try
            {
                HWin.RoiCell TempRoiCell = new HWin.RoiCell();
                hWin1_Roilist = hWin1.RoiCellList;

                TempRoiCell.RoiName = RoiName;
                TempRoiCell.RoiPix = RoiPix;
                hWin1_Roilist.Add(RoiName, TempRoiCell);

                //修改Roi           
                Roi TempRoi = new Roi();

                TempRoi.RoiName = TempRoiCell.RoiName;
                TempRoi.RoiType = TempRoiCell.RoiPix.RoiType;
                Cam1LocRoiY1 = TempRoi.Row1 = TempRoiCell.RoiPix.Y1;
                Cam1LocRoiX1 = TempRoi.Column1 = TempRoiCell.RoiPix.X1;
                Cam1LocRoiY2 = TempRoi.Row2 = TempRoiCell.RoiPix.Y2;
                Cam1LocRoiX2 = TempRoi.Column2 = TempRoiCell.RoiPix.X2;

                hWin1.DisPlay(hWin1.HObj.Clone(), null, string.Empty, true, true, false);
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
        private void hWin1_SetRoi()
        {
            hWin1.RoiCellList = hWin1_Roilist;
        }


        #region 自定义函数
        private void ProcessMainDlgMsg(string Message, object data)
        {
            try
            {
                switch (Message)
                {
                    case "DispImgToProductDlgW0":
                        hWin0.DisPlay(FormMain.ho_SrcImg[0].Clone(), null, string.Empty);
                        break;


                    case "DispImgToProductDlgW1":
                        hWin1.DisPlay(FormMain.ho_SrcImg[1].Clone(), null, string.Empty);
                        break;

                    case "mainDlgGrabDoneCam0":
                        hWin0.DisPlay(FormMain.ho_SrcImg[0].Clone(), null, string.Empty);
                        MessageBox.Show("获取图片成功");
                        break;


                    case "mainDlgGrabDoneCam1":
                        hWin1.DisPlay(FormMain.ho_SrcImg[1].Clone(), null, string.Empty);
                        MessageBox.Show("获取图片成功");
                        break;

                    case "DispCam0Rst":
                        HObject ho_DispObj = (HObject)data;
                        hWin0.DisPlay(ho_DispObj.Clone(), null, string.Empty);
                        break;
                    case "DispCam1Rst":
                        HObject ho_DispObj1 = (HObject)data;
                        hWin1.DisPlay(ho_DispObj1.Clone(), null, string.Empty);
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


        private void loadProductToComboBox()
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

                string strLstNam = string.Empty;

                //获取上次退出软件时所选择的产品号
                if (File.Exists(FormMain.strComParIniPath))
                {
                    InIClass getLastName = new InIClass(FormMain.strComParIniPath);

                    strLstNam = getLastName.Read("ProductName", "Exit");

                }

                int selectItem = 0;
                HTuple hv_Length = 0;
                HOperatorSet.ReadTuple("productName.tup", out hv_ProductName);
                HOperatorSet.TupleLength(hv_ProductName, out hv_Length);

                comboBox_select_product.Items.Clear();
                for (int i = 0; i < hv_Length; i++)
                {
                    string strProductName = hv_ProductName[i];
                    if (strLstNam == strProductName)
                    {
                        selectItem = i;
                    }
                    comboBox_select_product.Items.Add(strProductName);
                }

                if (comboBox_select_product.Items.Count > 0)
                {
                    comboBox_select_product.SelectedIndex = selectItem;
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

        #region UI

        #region 顶部
        private void radioButton_edit_CheckedChanged(object sender, EventArgs e)
        {
            comboBox_select_product.Enabled = true;
            textBox_product_type.Enabled = false;
            button_add_product.Enabled = false;
            button_delete_product.Enabled = true;
            button_save.Enabled = true;
        }

        private void radioButton_new_CheckedChanged(object sender, EventArgs e)
        {
            comboBox_select_product.Enabled = false;
            textBox_product_type.Enabled = true;
            button_add_product.Enabled = true;
            button_delete_product.Enabled = false;
            button_save.Enabled = false;
        }

        private void button_delete_product_Click(object sender, EventArgs e)
        {
            return;
            try
            {
                string strCurItem = comboBox_select_product.SelectedItem.ToString();

                if (MessageBox.Show("您确定要删除产品    " + strCurItem + "    吗？", "警告", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question) == DialogResult.OK)
                {
                    FormMain.DeleteDirSafely("product/" + strCurItem);

                    HOperatorSet.ReadTuple("productName.tup", out hv_ProductName);
                    HOperatorSet.TupleRemove(hv_ProductName, comboBox_select_product.SelectedIndex, out hv_ProductName);
                    HOperatorSet.WriteTuple(hv_ProductName, "productName.tup");
                    loadProductToComboBox();
                    productDlgSendToMainDlg("updataProductList", "");
                    MessageBox.Show("产品 " + strCurItem + " 已删除！");
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

        private void button_add_product_Click(object sender, EventArgs e)
        {
            return;
            if (textBox_product_type.Text == string.Empty)
            {
                MessageBox.Show("请输入产品名称");
                return;
            }

            int NameRepetition = -1;
            NameRepetition = comboBox_select_product.FindString(textBox_product_type.Text);
            if (NameRepetition > -1)
            {
                MessageBox.Show("您输入的产品名称已经存在");
                return;
            }

            FormMain.g_iChangeproduct = 0;
            try
            {
                HOperatorSet.ReadTuple("ProductName.tup", out hv_ProductName);
                HOperatorSet.TupleInsert(hv_ProductName, 0, textBox_product_type.Text, out hv_ProductName);
                HOperatorSet.WriteTuple(hv_ProductName, "productName.tup");
                loadProductToComboBox();
                MessageBox.Show("新产品名称 " + textBox_product_type.Text + " 添加成功!", "提示");


                textBox_product_type.Text = string.Empty;
                textBox_product_type.Enabled = false;
                button_add_product.Enabled = false;
                radioButton_edit.Select();
                comboBox_select_product.Enabled = true;
                button_delete_product.Enabled = true;
                productDlgSendToMainDlg("updataProductList", "");
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

        private void button_save_Click(object sender, EventArgs e)
        {
            try
            {
                string strProductName = string.Empty;
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


                    string IniPath = string.Empty;

                    //其它参数
                    if (!File.Exists("product/" + strProductName + "/other.ini"))
                    {
                        FileStream fs = File.Create("product/" + strProductName + "/other.ini");
                        fs.Close();
                    }

                    IniPath = "product/" + strProductName + "/other.ini";

                    InIClass IniOther = new InIClass(IniPath);

                    IniOther.Write("other", "DepartmentCode", txt_Department.Text);                //部门代码
                    IniOther.Write("other", "MachineCode", textBox_MachineID.Text);                     //设备编号
                    IniOther.Write("other", "Batch", textBox_Batch.Text);                               //物料批号                 

                    //cam0 参数
                    if (!File.Exists("product/" + strProductName + "/cam0par.ini"))
                    {
                        FileStream fs = File.Create("product/" + strProductName + "/cam0par.ini");
                        fs.Close();
                    }

                    IniPath = "product/" + strProductName + "/cam0par.ini";
                    InIClass Inicam0par = new InIClass(IniPath);
                    Inicam0par.Write("product", "roi_num", comboBox_cam0_roi.Items.Count.ToString());//ROI数量
                    for (int i = 1; i <= comboBox_cam0_roi.Items.Count; i++)    //加载ROI
                    {
                        string iniSection = "Load-" + i.ToString();
                        if (hWin0_Roilist.ContainsKey(iniSection))
                        {
                            HWin.RoiCell tmpRoi = new HWin.RoiCell();
                            hWin0_Roilist.TryGetValue(iniSection, out tmpRoi);   //以键获取值
                            Inicam0par.Write(iniSection, "RoiType", 0.ToString());
                            Inicam0par.Write(iniSection, "x1", tmpRoi.RoiPix.X1.ToString());
                            Inicam0par.Write(iniSection, "x2", tmpRoi.RoiPix.X2.ToString());
                            Inicam0par.Write(iniSection, "y1", tmpRoi.RoiPix.Y1.ToString());
                            Inicam0par.Write(iniSection, "y2", tmpRoi.RoiPix.Y2.ToString());
                        }
                    }
                    //Cam0 视觉参数
                    Inicam0par.Write("Vision", "Exposure0", UpDown_Cam0_ExposureTime.Text); //曝光时间      
                    Inicam0par.Write("Vision", "Pix2MM", UpDownCam0_Pix2MM.Text); //像素当量
                    Inicam0par.Write("Vision", "GrayMin", numericUD_Cam0GrayMin.Text); //最小灰度值
                    Inicam0par.Write("Vision", "CodeType", combCodeType.Text); //
                    Inicam0par.Write("Vision", "LocMethod", CombLocMethod.Text); //定位方法
                   

                  

                    //标准放料点位
                    Inicam0par.Write("Load", "StdA-1X", Std_AX1.Text);
                    Inicam0par.Write("Load", "StdA-1Y", Std_AY1.Text);
                    Inicam0par.Write("Load", "StdA-1angle", Std_Aangle1.Text);

                    Inicam0par.Write("Load", "StdA-2X", Std_AX2.Text);
                    Inicam0par.Write("Load", "StdA-2Y", Std_AY2.Text);
                    Inicam0par.Write("Load", "StdA-2angle", Std_Aangle2.Text);

                    Inicam0par.Write("Load", "StdA-3X", Std_AX3.Text);
                    Inicam0par.Write("Load", "StdA-3Y", Std_AY3.Text);
                    Inicam0par.Write("Load", "StdA-3angle", Std_Aangle3.Text);


                    Inicam0par.Write("Load", "StdB-1X", Std_BX1.Text);
                    Inicam0par.Write("Load", "StdB-1Y", Std_BY1.Text);
                    Inicam0par.Write("Load", "StdB-1angle", Std_Bangle1.Text);

                    Inicam0par.Write("Load", "StdB-2X", Std_BX2.Text);
                    Inicam0par.Write("Load", "StdB-2Y", Std_BY2.Text);
                    Inicam0par.Write("Load", "StdB-2angle", Std_Bangle2.Text);

                    Inicam0par.Write("Load", "StdB-3X", Std_BX3.Text);
                    Inicam0par.Write("Load", "StdB-3Y", Std_BY3.Text);
                    Inicam0par.Write("Load", "StdB-3angle", Std_Bangle3.Text);
                    //6吸嘴旋转中心
                    Inicam0par.Write("RotateRC", "1RotateR", numUD_1RotateR.Text);
                    Inicam0par.Write("RotateRC", "1RotateC", numUD_1RotateC.Text);
                    Inicam0par.Write("RotateRC", "2RotateR", numUD_2RotateR.Text);
                    Inicam0par.Write("RotateRC", "2RotateC", numUD_2RotateC.Text);
                    Inicam0par.Write("RotateRC", "3RotateR", numUD_3RotateR.Text);
                    Inicam0par.Write("RotateRC", "3RotateC", numUD_3RotateC.Text);
                    Inicam0par.Write("RotateRC", "4RotateR", numUD_4RotateR.Text);
                    Inicam0par.Write("RotateRC", "4RotateC", numUD_4RotateC.Text);
                    Inicam0par.Write("RotateRC", "5RotateR", numUD_5RotateR.Text);
                    Inicam0par.Write("RotateRC", "5RotateC", numUD_5RotateC.Text);
                    Inicam0par.Write("RotateRC", "6RotateR", numUD_6RotateR.Text);
                    Inicam0par.Write("RotateRC", "6RotateC", numUD_6RotateC.Text);

                    //cam1 参数
                    if (!File.Exists("product/" + strProductName + "/cam1par.ini"))
                    {
                        FileStream fs = File.Create("product/" + strProductName + "/cam1par.ini");
                        fs.Close();
                    }

                    IniPath = "product/" + strProductName + "/cam1par.ini";

                    InIClass Inicam1par = new InIClass(IniPath);
                    Inicam1par.Write("product", "roi_num", comboBox_cam1_roi.Items.Count.ToString());//ROI数量

                    for (int i = 1; i <= comboBox_cam1_roi.Items.Count; i++)    //加载ROI
                    {
                        string iniSection = "Mark" + i.ToString();
                        if (hWin1_Roilist.ContainsKey(iniSection))
                        {
                            HWin.RoiCell tmpRoi = new HWin.RoiCell();
                            hWin1_Roilist.TryGetValue(iniSection, out tmpRoi);   //以键获取值
                            Inicam1par.Write(iniSection, "RoiType", 0.ToString());
                            Inicam1par.Write(iniSection, "x1", tmpRoi.RoiPix.X1.ToString());
                            Inicam1par.Write(iniSection, "x2", tmpRoi.RoiPix.X2.ToString());
                            Inicam1par.Write(iniSection, "y1", tmpRoi.RoiPix.Y1.ToString());
                            Inicam1par.Write(iniSection, "y2", tmpRoi.RoiPix.Y2.ToString());
                        }
                    }
                    //Cam1 视觉参数
                    Inicam1par.Write("Vision", "Exposure1", UpDown_Cam1_ExposureTime.Text);               //曝光时间                                   
                    Inicam1par.Write("Vision", "Pix2MM", UpDownCam1_Pix2MM.Text);                         //像素当量
                    Inicam1par.Write("Vision", "CoordAngleOffset", UpDown_Cam1_CoordAngleOffset.Text);    //坐标系补偿角度

                    Inicam1par.Write("GRR", "MultiLine", cmb_MultiLine.Text); //GRR模式的多线测量
                    Inicam1par.Write("GRR", "MultiAngle", numericUpDown_MultiAngle.Text); //GRR模式的多线测量

                    Inicam1par.Write("OFFSET", "AAoffset", NDU_AAoff.Text);
                    Inicam1par.Write("OFFSET", "BBoffset", NDU_BBoff.Text);
                    Inicam1par.Write("OFFSET", "CCoffset", NDU_CCoff.Text);
                    Inicam1par.Write("OFFSET", "DDoffset", NDU_DDoff.Text);

                    //Cam1 工艺参数
                    Inicam1par.Write("TechParam", "upperDeviation", UpDown_upperDeviation.Text);
                    Inicam1par.Write("TechParam", "lowerDeviation", UpDown_lowerDeviation.Text);
                    Inicam1par.Write("TechParam", "Dstd", UpDown_DiaStd.Text);


                    productDlgSendToMainDlg("iniRoiParam", "");

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
        /// <summary>
        /// 产品变更 读取参数事件方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_select_product_SelectedIndexChanged(object sender, EventArgs e)
        {
            return;
            try
            {
                InitRoi();
                InitVisionParam();

                string strProductName = string.Empty;
                strProductName = comboBox_select_product.SelectedItem.ToString();
                //保存当前所选择的项(产品号),下次开软件自动加载该项产品
                if (File.Exists(FormMain.strComParIniPath))
                {
                    InIClass getLastName = new InIClass(FormMain.strComParIniPath);

                    getLastName.Write("ProductName", "Exit", strProductName);

                    if (productDlgSendToMainDlg != null)
                    {
                        productDlgSendToMainDlg("setProductName", comboBox_select_product.Text);
                    }
                }

                //产品名称
                FormMain.g_strProdcutName = comboBox_select_product.Text;


                //***第一次打开软件也会弹出
                if (FormMain.g_iChangeproduct != 0)
                {
                    MessageBox.Show("  换型成功！  ", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                FormMain.g_iChangeproduct++;

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

        #region cam0 page 
        private void button_set_light1_Click(object sender, EventArgs e)
        {
            // FormMain.g_iLightLuminanceCam0 = (int)numericUpDown_light0.Value;

            // button_set_light0.Text = "设为运行时亮度 (当前=" + trackBar_light0.Value.ToString() + ")";
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
                    HOperatorSet.ReadImage(out FormMain.ho_SrcImg[0], ofd.FileName);
                    // HOperatorSet.MedianRect(SrcImg, out SrcImg, 3.5, 3.5);

                    HTuple chanels = new HTuple();
                    HOperatorSet.CountChannels(FormMain.ho_SrcImg[0], out chanels);
                    if (chanels == 3)
                    {
                        HObject r, g, b;
                        HOperatorSet.Decompose3(FormMain.ho_SrcImg[0], out r, out g, out b);
                        HOperatorSet.Rgb3ToGray(r, g, b, out FormMain.ho_SrcImg[0]);
                    }

                    hWin0.DisPlay(FormMain.ho_SrcImg[0].Clone(), null, string.Empty);
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
            if (FormMain.g_bCam0ContinueGrab)
            {
                FormMain.g_bCam0DispToPrductDlg = !FormMain.g_bCam0DispToPrductDlg;
                if (FormMain.g_bCam0DispToPrductDlg)
                {
                    button_cam0_continue_grab.Text = "关闭同步";
                }
                else
                {
                    button_cam0_continue_grab.Text = "画面同步";
                }
            }
            else
            {
                MessageBox.Show("请先打开主窗口的连续采集");
            }
        }

        private void button_cam0_grab_Click(object sender, EventArgs e)
        {
            productDlgSendToMainDlg("cam0GrabImage", "");


        }

        private void button_cam0_save_image_Click(object sender, EventArgs e)
        {
            if (FormMain.ho_SrcImg[0].IsInitialized())
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "保存图片";
                sfd.Filter = "图片|*.bmp;*.jpg";

                try
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        if (FormMain.ho_SrcImg[0].IsInitialized())
                        {
                            HOperatorSet.WriteImage(FormMain.ho_SrcImg[0], "bmp", 0, sfd.FileName);
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

        private void button_cam0_test_Click(object sender, EventArgs e)
        {
            try
            {
                productDlgSendToMainDlg("TestCam0", "");
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

        private void button_cam0_save_result_Click(object sender, EventArgs e)
        {

        }

        private void button_cam0_fit_Click(object sender, EventArgs e)
        {
            if (FormMain.ho_SrcImg[0].IsInitialized())
            {
                hWin0.DisPlay(FormMain.ho_SrcImg[0].Clone(), null, string.Empty);
            }
        }
        #endregion

        #region cam1 page
        private void button_light1_set_Click(object sender, EventArgs e)
        {
            //FormMain.g_iLightLuminanceCam1 = trackBar_light1.Value;

            //button_set_light1.Text = "设为运行时亮度 (当前=" + trackBar_light1.Value.ToString() + ")";
        }

        private void button_cam1_deep_set_Click(object sender, EventArgs e)
        {

        }

        private void button_cam1_add_roi_Click(object sender, EventArgs e)
        {
            return;
            if (hWin1.HObj.IsInitialized())
            {
                for (int i = 1; i < 100; i++)
                {
                    string RoiName = "Mark" + i.ToString();

                    if (!hWin1.RoiCellList.Keys.Contains(RoiName))
                    {
                        hWin1.AddRoi(RoiName, 0);
                        //Roi TempRoi = new Roi();
                        //cam1Roi.Add(RoiName, TempRoi);
                        comboBox_cam1_roi.Items.Add(RoiName);
                        comboBox_cam1_roi.SelectedItem = RoiName;
                        break;
                    }
                }
            }



        }

        private void button_cam1_select_roi_Click(object sender, EventArgs e)
        {
            try
            {
                if (comboBox_cam1_roi.SelectedIndex == -1)
                {
                    return;
                }

                string Name = comboBox_cam1_roi.SelectedItem.ToString();
                hWin1.OuterSelectRoi(Name);
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

        private void button_cam1_delete_roi_Click(object sender, EventArgs e)
        {
            return;
            try
            {
                if (comboBox_cam1_roi.SelectedIndex == -1)
                {
                    return;
                }
                //if (comboBox_cam1_roi.SelectedItem.ToString() == "LocRoi")
                //{
                //    MessageBox.Show("定位框不允许删除！");
                //    return;
                //}

                string selectName = comboBox_cam1_roi.SelectedItem.ToString();
                hWin1_Roilist.Remove(selectName);
                hWin1.RoiCellList = hWin1_Roilist;

                //cam1Roi.Remove(selectName);         
                comboBox_cam1_roi.Items.Remove(selectName);
                if (comboBox_cam1_roi.Items.Count > 0)
                {
                    comboBox_cam1_roi.SelectedIndex = 0;
                }

                hWin1.DisPlay(hWin1.HObj.Clone(), null, string.Empty);
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
            if (FormMain.g_bCam1ContinueGrab)
            {
                FormMain.g_bCam1DispToPrductDlg = !FormMain.g_bCam1DispToPrductDlg;
                if (FormMain.g_bCam1DispToPrductDlg)
                {
                    button_cam1_continue_grab.Text = "关闭同步";
                }
                else
                {
                    button_cam1_continue_grab.Text = "画面同步";
                }
            }
            else
            {
                MessageBox.Show("请先打开主窗口的连续采集");
            }
        }

        private void button_cam1_grab_Click(object sender, EventArgs e)
        {
            productDlgSendToMainDlg("cam1GrabImage", "");
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
                    HOperatorSet.ReadImage(out FormMain.ho_SrcImg[1], ofd.FileName);

                    HTuple chanels = new HTuple();
                    HOperatorSet.CountChannels(FormMain.ho_SrcImg[1], out chanels);
                    if (chanels == 3)
                    {
                        HObject r, g, b;
                        HOperatorSet.Decompose3(FormMain.ho_SrcImg[1], out r, out g, out b);
                        HOperatorSet.Rgb3ToGray(r, g, b, out FormMain.ho_SrcImg[1]);
                    }

                    hWin1.DisPlay(FormMain.ho_SrcImg[1].Clone(), null, string.Empty);
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

        private void Btn_Model_Click(object sender, EventArgs e)
        {
            try
            {
                if (!TestDetector.isInitialed)
                {
                    MessageBox.Show("未设置模板");
                    return;
                }
                SrcImg = TestDetector.ho_ModelImage.Clone();
                if (SrcImg == null || !SrcImg.IsInitialized()) return;
                //*
                string strProductName = string.Empty;
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
                    //cam0 参数
                    if (!File.Exists("product/" + strProductName + "/cam0par.ini"))
                    {
                        FileStream fs = File.Create("product/" + strProductName + "/cam0par.ini");
                        fs.Close();
                    }

                    string IniPath = "product/" + strProductName + "/cam0par.ini";
                    InIClass Inicam0par = new InIClass(IniPath);
                    UpDown_Cam0_ExposureTime.Text = Inicam0par.Read("Exposure", "Exposure0");
                    int num = Convert.ToInt32(Inicam0par.Read("product", "roi_num"));
                    TestDetector.ho_ROILoads.Dispose();
                    for (int i = 1; i < num + 1; i++)
                    {
                        string iniSection = "Load-" + i.ToString();
                        HWin.RoiCell TempRoiLoad = new HWin.RoiCell();
                        HWin.Roi TempRectPix = new HWin.Roi();
                        TempRoiLoad.RoiName = iniSection;
                        TempRectPix.RoiType = Convert.ToInt32(Inicam0par.Read(iniSection, "RoiType"));
                        TempRectPix.Y1 = Convert.ToInt32(Inicam0par.Read(iniSection, "y1"));
                        TempRectPix.X1 = Convert.ToInt32(Inicam0par.Read(iniSection, "x1"));
                        TempRectPix.Y2 = Convert.ToInt32(Inicam0par.Read(iniSection, "y2"));
                        TempRectPix.X2 = Convert.ToInt32(Inicam0par.Read(iniSection, "x2"));
                        TempRoiLoad.RoiPix = TempRectPix;
                        hWin0_Roilist.Add(TempRoiLoad.RoiName, TempRoiLoad);
                        comboBox_cam0_roi.Items.Add(iniSection);
                        TestDetector.genRectRoi(TempRectPix.Y1, TempRectPix.X1, TempRectPix.Y2, TempRectPix.X2, out TestDetector.ho_ROILoad);
                        HOperatorSet.ConcatObj(TestDetector.ho_ROILoads, TestDetector.ho_ROILoad, out TestDetector.ho_ROILoads);
                    }
                    //定位失败   (二维码+mark点+圆心)
                    if (!TestDetector.LocationCap(SrcImg.Clone(), FormMain.ProgName, CombLocMethod.SelectedItem.ToString(), "true",
                        TestDetector.ho_ROILoads))
                    {
                        MessageBox.Show("定位失败");
                        return;
                    }

                    hWin0.DisPlay(SrcImg.Clone(), null, "", true, true, true);

                    if (SrcImg != null) SrcImg.Dispose();

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

        private void button_cam0_add_roi_Click(object sender, EventArgs e)
        {
            return;
            if (hWin0.HObj.IsInitialized())
            {
                for (int i = 1; i < 100; i++)
                {
                    string RoiName = "Load-" + i.ToString();

                    if (!hWin0.RoiCellList.Keys.Contains(RoiName))
                    {
                        hWin0.AddRoi(RoiName, 0);
                        //Roi TempRoi = new Roi();
                        //cam1Roi.Add(RoiName, TempRoi);
                        comboBox_cam0_roi.Items.Add(RoiName);
                        comboBox_cam0_roi.SelectedItem = RoiName;
                        break;
                    }
                }
            }
        }

        private void button_cam0_select_roi_Click(object sender, EventArgs e)
        {
            try
            {
                if (comboBox_cam0_roi.SelectedIndex == -1)
                {
                    return;
                }

                string Name = comboBox_cam0_roi.SelectedItem.ToString();
                hWin0.OuterSelectRoi(Name);
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

        private void button_cam0_delete_roi_Click(object sender, EventArgs e)
        {
            return;
            try
            {
                if (comboBox_cam0_roi.SelectedIndex == -1)
                {
                    return;
                }
                //if (comboBox_cam1_roi.SelectedItem.ToString() == "LocRoi")
                //{
                //    MessageBox.Show("定位框不允许删除！");
                //    return;
                //}

                string selectName = comboBox_cam0_roi.SelectedItem.ToString();
                hWin0_Roilist.Remove(selectName);
                hWin0.RoiCellList = hWin1_Roilist;
                comboBox_cam0_roi.Items.Remove(selectName);
                if (comboBox_cam0_roi.Items.Count > 0)
                {
                    comboBox_cam0_roi.SelectedIndex = 0;
                }
                if (comboBox_cam0_roi.Items.Count == 0)
                {
                    comboBox_cam0_roi.Text = "";
                }

                hWin0.DisPlay(hWin0.HObj.Clone(), null, string.Empty);
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

        private void btnSetModel_Click_Click(object sender, EventArgs e)
        {
            //*
            string strProductName = string.Empty;
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
                //cam0 参数
                if (!File.Exists("product/" + strProductName + "/cam0par.ini"))
                {
                    FileStream fs = File.Create("product/" + strProductName + "/cam0par.ini");
                    fs.Close();
                }

                string IniPath = "product/" + strProductName + "/cam0par.ini";
                InIClass Inicam0par = new InIClass(IniPath);
                UpDown_Cam0_ExposureTime.Text = Inicam0par.Read("Exposure", "Exposure0");
                int num = Convert.ToInt32(Inicam0par.Read("product", "roi_num"));
                TestDetector.ho_ROILoads.Dispose();
                for (int i = 1; i < num + 1; i++)
                {
                    string iniSection = "Load-" + i.ToString();
                    HWin.RoiCell TempRoiLoad = new HWin.RoiCell();
                    HWin.Roi TempRectPix = new HWin.Roi();
                    TempRoiLoad.RoiName = iniSection;
                    TempRectPix.RoiType = Convert.ToInt32(Inicam0par.Read(iniSection, "RoiType"));
                    TempRectPix.Y1 = Convert.ToInt32(Inicam0par.Read(iniSection, "y1"));
                    TempRectPix.X1 = Convert.ToInt32(Inicam0par.Read(iniSection, "x1"));
                    TempRectPix.Y2 = Convert.ToInt32(Inicam0par.Read(iniSection, "y2"));
                    TempRectPix.X2 = Convert.ToInt32(Inicam0par.Read(iniSection, "x2"));
                    TempRoiLoad.RoiPix = TempRectPix;
                    hWin0_Roilist.Add(TempRoiLoad.RoiName, TempRoiLoad);
                    comboBox_cam0_roi.Items.Add(iniSection);
                    TestDetector.genRectRoi(TempRectPix.Y1, TempRectPix.X1, TempRectPix.Y2, TempRectPix.X2, out TestDetector.ho_ROILoad);
                    HOperatorSet.ConcatObj(TestDetector.ho_ROILoads, TestDetector.ho_ROILoad, out TestDetector.ho_ROILoads);
                }
                //找出二维码坐标 作为测量时markRoi的锚点
                //1、二维码类型

            }
        }

        private void hWin0_Load(object sender, EventArgs e)
        {

        }

        private void button_cam1_openModel_Click(object sender, EventArgs e)
        {


            try
            {
                string strProductName = string.Empty;
                strProductName = comboBox_select_product.SelectedItem.ToString();
                if (comboBox_select_product.SelectedItem.ToString() != string.Empty)

                    //cam1 参数
                    if (!File.Exists("product/" + strProductName + "/cam1par.ini"))
                    {
                        FileStream fs = File.Create("product/" + strProductName + "/cam1par.ini");
                        fs.Close();
                    }
                string IniPath = string.Empty;
                string ImagePath = string.Empty;

                IniPath = "product/" + strProductName + "/cam1par.ini";
                ImagePath = "product/" + strProductName + "/ModelImage.bmp";

                InIClass Inicam1par = new InIClass(IniPath);
                HOperatorSet.ReadImage(out HObject ModelImage, ImagePath);

                HTuple chanels = new HTuple();
                HOperatorSet.CountChannels(ModelImage, out chanels);
                if (chanels == 3)
                {
                    HObject r, g, b;
                    HOperatorSet.Decompose3(SrcImg, out r, out g, out b);
                    HOperatorSet.Rgb3ToGray(r, g, b, out ModelImage);
                }
               
               FormMain.ho_SrcImg[1] = ModelImage;
                hWin1.DisPlay(ModelImage.Clone(), null, string.Empty);

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

     

        private void btn_OnOffParam_Click(object sender, EventArgs e)
        {
            FrmLogin frmLogin = new FrmLogin();
            if (frmLogin.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            else
            {
                if (btn_OnOffParam.Text=="参数编辑开关-- > 开")
                {
                    tabPage_cam0_par.Parent = TabControlSet;
                    tabPage_cam1_par.Parent = TabControlSet;
                    TabControlSet.SelectedIndex = 0;
                    btn_OnOffParam.Text = "参数编辑开关-- > 关";
                }
                else
                {
                    tabPage_cam0_par.Parent = null;
                    tabPage_cam1_par.Parent = null;
                    btn_OnOffParam.Text = "参数编辑开关-- > 开";
                }

               
            }
        }

        private void radioButton_edit_CheckedChanged_2(object sender, EventArgs e)
        {
            comboBox_select_product.Enabled = true;
            textBox_product_type.Enabled = false;
            button_add_product.Enabled = false;
            button_delete_product.Enabled = true;
            button_save.Enabled = true;
            tabPage_cam0_par.Parent = tabControl1;
            tabPage_cam1_par.Parent = tabControl1;
        }

        private void radioButton_new_CheckedChanged_2(object sender, EventArgs e)
        {
            comboBox_select_product.Enabled = false;
            textBox_product_type.Enabled = true;
            button_add_product.Enabled = true;
            button_delete_product.Enabled = false;
            button_save.Enabled = false;
        }

        private void btn_NGChange_Click(object sender, EventArgs e)
        {
            if (FormMain.g_iMachineState==(int)MachineState.Run)
            {
                MessageBox.Show("正在运行中，请停止运行后再设置", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                if (btn_NGChange.Text == "B品回OK料盘")
                {
                    UpDown_upperDeviation.Value = 1;
                    btn_NGChange.Text = "改回正常生产模式";
                    FormMain.g_bNgChange = true;
                }
                else
                {
                    UpDown_upperDeviation.Value = 3 / 100;
                    btn_NGChange.Text = "B品回OK料盘";
                    FormMain.g_bNgChange = false;
                }
            }
            
        }

        private void button_cam1_test_image_Click(object sender, EventArgs e)
        {

        }

        private void button_cam1_save_image_Click(object sender, EventArgs e)
        {
            if (FormMain.ho_SrcImg[1].IsInitialized())
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "保存图片";
                sfd.Filter = "图片|*.bmp;*.jpg";

                try
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        if (FormMain.ho_SrcImg[1].IsInitialized())
                        {
                            HOperatorSet.WriteImage(FormMain.ho_SrcImg[1], "bmp", 0, sfd.FileName);
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

        private void button_cam1_save_result_Click(object sender, EventArgs e)
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
            if (FormMain.ho_SrcImg[1].IsInitialized())
            {
                hWin1.DisPlay(FormMain.ho_SrcImg[1].Clone(), null, string.Empty);
            }
        }

        #endregion

        #endregion

        #region VisionOperation

        private void button_cam1_loc_Click(object sender, EventArgs e)
        {
            try
            {

                if (MessageBox.Show("您确定要重新创建模板    " + comboBox_select_product.Text + "    吗？", "警告", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question) == DialogResult.OK)
                {
                    productDlgSendToMainDlg("SaveCam1Model", "");
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

        private void button_cam1_test_Click(object sender, EventArgs e)
        {
            try
            {
                productDlgSendToMainDlg("TestCam1", "");
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
    }
}
#endregion

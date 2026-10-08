<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Form1))
        Me.tabMainFunctions = New System.Windows.Forms.TabControl()
        Me.tabConfiguration = New System.Windows.Forms.TabPage()
        Me.grpProjects = New System.Windows.Forms.GroupBox()
        Me.btnPickRoot = New System.Windows.Forms.Button()
        Me.btnSaveConfiguration2 = New System.Windows.Forms.Button()
        Me.lstAllProjects = New System.Windows.Forms.ListView()
        Me.colProjectName = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.colLastEdit = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.btnNewProject = New System.Windows.Forms.Button()
        Me.btnRenameProject = New System.Windows.Forms.Button()
        Me.btnLoadProject = New System.Windows.Forms.Button()
        Me.btnDeleteProject = New System.Windows.Forms.Button()
        Me.btnSaveAs = New System.Windows.Forms.Button()
        Me.grpSimulationSettings = New System.Windows.Forms.GroupBox()
        Me.Label61 = New System.Windows.Forms.Label()
        Me.numTimeDecayFactor = New System.Windows.Forms.NumericUpDown()
        Me.numSimulationSteps = New System.Windows.Forms.NumericUpDown()
        Me.lblInfo = New System.Windows.Forms.Label()
        Me.chkUse4thOrderLaplacian = New System.Windows.Forms.CheckBox()
        Me.chkUseGPU = New System.Windows.Forms.CheckBox()
        Me.numQuality = New System.Windows.Forms.NumericUpDown()
        Me.lblVideoQuality = New System.Windows.Forms.Label()
        Me.numIntensityScaling = New System.Windows.Forms.NumericUpDown()
        Me.numOffsetIntensity = New System.Windows.Forms.NumericUpDown()
        Me.numOffsetField = New System.Windows.Forms.NumericUpDown()
        Me.numFieldScaling = New System.Windows.Forms.NumericUpDown()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.numScaleIntensityValues = New System.Windows.Forms.Label()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.lblOffsetField = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.cmbIntensityColorScheme = New System.Windows.Forms.ComboBox()
        Me.cmbFieldColorScheme = New System.Windows.Forms.ComboBox()
        Me.chkPreviewVideoIntensity = New System.Windows.Forms.CheckBox()
        Me.chkExportVideoIntensity = New System.Windows.Forms.CheckBox()
        Me.chkPreviewVideoField = New System.Windows.Forms.CheckBox()
        Me.chkExportVideoField = New System.Windows.Forms.CheckBox()
        Me.lblPreviewVideo = New System.Windows.Forms.Label()
        Me.lblExportVideo = New System.Windows.Forms.Label()
        Me.numVisualizeEvery = New System.Windows.Forms.NumericUpDown()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.lblVisualizeEvery = New System.Windows.Forms.Label()
        Me.Label7 = New System.Windows.Forms.Label()
        Me.lblSimulationSteps = New System.Windows.Forms.Label()
        Me.grpSceneProperties = New System.Windows.Forms.GroupBox()
        Me.Label20 = New System.Windows.Forms.Label()
        Me.numRefractiveIndex = New System.Windows.Forms.NumericUpDown()
        Me.picImagePreview = New System.Windows.Forms.PictureBox()
        Me.lblScaleImgSourceIntensity = New System.Windows.Forms.Label()
        Me.numScaleImageSource = New System.Windows.Forms.NumericUpDown()
        Me.btnEditObjectSourceList = New System.Windows.Forms.Button()
        Me.txtDampingEdgeSize = New System.Windows.Forms.TextBox()
        Me.chkUseObjectSources = New System.Windows.Forms.CheckBox()
        Me.chkUseImageSources = New System.Windows.Forms.CheckBox()
        Me.chkDampingEdge = New System.Windows.Forms.CheckBox()
        Me.btnLoadSceneImage = New System.Windows.Forms.Button()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.lblPix = New System.Windows.Forms.Label()
        Me.lblX = New System.Windows.Forms.Label()
        Me.txtYsize = New System.Windows.Forms.TextBox()
        Me.txtXSize = New System.Windows.Forms.TextBox()
        Me.radUserDefinedScene = New System.Windows.Forms.RadioButton()
        Me.radUseSceneImage = New System.Windows.Forms.RadioButton()
        Me.lblImageNameAndSize = New System.Windows.Forms.Label()
        Me.tabSourceList = New System.Windows.Forms.TabPage()
        Me.Label62 = New System.Windows.Forms.Label()
        Me.Label60 = New System.Windows.Forms.Label()
        Me.chkAutoRescaleSourceOn = New System.Windows.Forms.CheckBox()
        Me.picPreviewSource = New System.Windows.Forms.PictureBox()
        Me.grpPlaceSource = New System.Windows.Forms.GroupBox()
        Me.pnlPosition = New System.Windows.Forms.Panel()
        Me.numSourcePositionX = New System.Windows.Forms.NumericUpDown()
        Me.Label36 = New System.Windows.Forms.Label()
        Me.lblPosition = New System.Windows.Forms.Label()
        Me.btnPickSourcePosition = New System.Windows.Forms.Button()
        Me.numSourcePositionY = New System.Windows.Forms.NumericUpDown()
        Me.Label35 = New System.Windows.Forms.Label()
        Me.btnDelete = New System.Windows.Forms.Button()
        Me.btnNew = New System.Windows.Forms.Button()
        Me.btnCopy = New System.Windows.Forms.Button()
        Me.lstSources = New System.Windows.Forms.ListBox()
        Me.pnlSourceImage = New System.Windows.Forms.Panel()
        Me.grpSourceDetails = New System.Windows.Forms.GroupBox()
        Me.txtDetachAfterSteps = New System.Windows.Forms.TextBox()
        Me.btnSaveSource = New System.Windows.Forms.Button()
        Me.Label47 = New System.Windows.Forms.Label()
        Me.chkDetachSource = New System.Windows.Forms.CheckBox()
        Me.lblLambdaIs = New System.Windows.Forms.Label()
        Me.chkSourceOn = New System.Windows.Forms.CheckBox()
        Me.grpComposite = New System.Windows.Forms.GroupBox()
        Me.Label59 = New System.Windows.Forms.Label()
        Me.Label58 = New System.Windows.Forms.Label()
        Me.numRandomizeOffWidth = New System.Windows.Forms.NumericUpDown()
        Me.numRandomizePosition = New System.Windows.Forms.NumericUpDown()
        Me.NumRandomizePhase = New System.Windows.Forms.NumericUpDown()
        Me.numRandomizeFrequency = New System.Windows.Forms.NumericUpDown()
        Me.chkRandomizePhase = New System.Windows.Forms.CheckBox()
        Me.chkRandomizePosition = New System.Windows.Forms.CheckBox()
        Me.chkOffWidth = New System.Windows.Forms.CheckBox()
        Me.chkRandomizeFrequency = New System.Windows.Forms.CheckBox()
        Me.numSpacingSizeY = New System.Windows.Forms.NumericUpDown()
        Me.numSpacingSizeX = New System.Windows.Forms.NumericUpDown()
        Me.numArraySizeY = New System.Windows.Forms.NumericUpDown()
        Me.numArraySizeX = New System.Windows.Forms.NumericUpDown()
        Me.Label50 = New System.Windows.Forms.Label()
        Me.Label51 = New System.Windows.Forms.Label()
        Me.Label52 = New System.Windows.Forms.Label()
        Me.Label53 = New System.Windows.Forms.Label()
        Me.cmbCompositeProperties = New System.Windows.Forms.ComboBox()
        Me.grpSingleSource = New System.Windows.Forms.GroupBox()
        Me.numOffStateCycles = New System.Windows.Forms.NumericUpDown()
        Me.numPhase = New System.Windows.Forms.NumericUpDown()
        Me.numAmplitude = New System.Windows.Forms.NumericUpDown()
        Me.numFrequency = New System.Windows.Forms.NumericUpDown()
        Me.numPulseWidthCycles = New System.Windows.Forms.NumericUpDown()
        Me.Label44 = New System.Windows.Forms.Label()
        Me.Label46 = New System.Windows.Forms.Label()
        Me.Label45 = New System.Windows.Forms.Label()
        Me.Label43 = New System.Windows.Forms.Label()
        Me.Label42 = New System.Windows.Forms.Label()
        Me.Label54 = New System.Windows.Forms.Label()
        Me.Label41 = New System.Windows.Forms.Label()
        Me.Label40 = New System.Windows.Forms.Label()
        Me.cmbPulseShape = New System.Windows.Forms.ComboBox()
        Me.radModePulsed = New System.Windows.Forms.RadioButton()
        Me.radModeContinuous = New System.Windows.Forms.RadioButton()
        Me.txtName = New System.Windows.Forms.TextBox()
        Me.cmbMainType = New System.Windows.Forms.ComboBox()
        Me.Label39 = New System.Windows.Forms.Label()
        Me.lblName = New System.Windows.Forms.Label()
        Me.Label38 = New System.Windows.Forms.Label()
        Me.tabProbeList = New System.Windows.Forms.TabPage()
        Me.GroupBox2 = New System.Windows.Forms.GroupBox()
        Me.Label49 = New System.Windows.Forms.Label()
        Me.Label48 = New System.Windows.Forms.Label()
        Me.Label37 = New System.Windows.Forms.Label()
        Me.grpProbe2 = New System.Windows.Forms.GroupBox()
        Me.Label22 = New System.Windows.Forms.Label()
        Me.numRunningWindow2 = New System.Windows.Forms.NumericUpDown()
        Me.chkProbeOn2 = New System.Windows.Forms.CheckBox()
        Me.txtStartAfterStep2 = New System.Windows.Forms.TextBox()
        Me.chkStartAfterStep2 = New System.Windows.Forms.CheckBox()
        Me.Label23 = New System.Windows.Forms.Label()
        Me.Label24 = New System.Windows.Forms.Label()
        Me.txtMaxScaleY2 = New System.Windows.Forms.TextBox()
        Me.txtMinScaleY2 = New System.Windows.Forms.TextBox()
        Me.radFullScale2 = New System.Windows.Forms.RadioButton()
        Me.radRunningScale2 = New System.Windows.Forms.RadioButton()
        Me.chkAutoScaleY2 = New System.Windows.Forms.CheckBox()
        Me.chkAutoScaleX2 = New System.Windows.Forms.CheckBox()
        Me.numBPosY2 = New System.Windows.Forms.NumericUpDown()
        Me.numAPosY2 = New System.Windows.Forms.NumericUpDown()
        Me.numBPosX2 = New System.Windows.Forms.NumericUpDown()
        Me.numAPosX2 = New System.Windows.Forms.NumericUpDown()
        Me.butPicPositions2 = New System.Windows.Forms.Button()
        Me.Label25 = New System.Windows.Forms.Label()
        Me.cmbPlaceOn2 = New System.Windows.Forms.ComboBox()
        Me.cmbMeasurement2 = New System.Windows.Forms.ComboBox()
        Me.Label26 = New System.Windows.Forms.Label()
        Me.Label27 = New System.Windows.Forms.Label()
        Me.cmbBoxSize2 = New System.Windows.Forms.ComboBox()
        Me.cmbProbeType2 = New System.Windows.Forms.ComboBox()
        Me.Label28 = New System.Windows.Forms.Label()
        Me.Label29 = New System.Windows.Forms.Label()
        Me.Label30 = New System.Windows.Forms.Label()
        Me.Label31 = New System.Windows.Forms.Label()
        Me.Label32 = New System.Windows.Forms.Label()
        Me.Label33 = New System.Windows.Forms.Label()
        Me.Label34 = New System.Windows.Forms.Label()
        Me.grpProbe1 = New System.Windows.Forms.GroupBox()
        Me.Label21 = New System.Windows.Forms.Label()
        Me.numRunningWindow = New System.Windows.Forms.NumericUpDown()
        Me.chkProbeOn1 = New System.Windows.Forms.CheckBox()
        Me.txtStartAfterStep = New System.Windows.Forms.TextBox()
        Me.chkStartAfterStep = New System.Windows.Forms.CheckBox()
        Me.Label19 = New System.Windows.Forms.Label()
        Me.Label18 = New System.Windows.Forms.Label()
        Me.txtMaxScaleY = New System.Windows.Forms.TextBox()
        Me.txtMinScaleY = New System.Windows.Forms.TextBox()
        Me.radFullScale = New System.Windows.Forms.RadioButton()
        Me.radRunningScale = New System.Windows.Forms.RadioButton()
        Me.chkAutoScaleY = New System.Windows.Forms.CheckBox()
        Me.chkAutoScaleX = New System.Windows.Forms.CheckBox()
        Me.numBPosY = New System.Windows.Forms.NumericUpDown()
        Me.numAPosY = New System.Windows.Forms.NumericUpDown()
        Me.numBPosX = New System.Windows.Forms.NumericUpDown()
        Me.numAPosX = New System.Windows.Forms.NumericUpDown()
        Me.butPicPositions = New System.Windows.Forms.Button()
        Me.Label16 = New System.Windows.Forms.Label()
        Me.cmbPlaceOn = New System.Windows.Forms.ComboBox()
        Me.cmbMeasurement = New System.Windows.Forms.ComboBox()
        Me.Label13 = New System.Windows.Forms.Label()
        Me.Label15 = New System.Windows.Forms.Label()
        Me.cmbBoxSize = New System.Windows.Forms.ComboBox()
        Me.cmbProbeType = New System.Windows.Forms.ComboBox()
        Me.Label14 = New System.Windows.Forms.Label()
        Me.Label12 = New System.Windows.Forms.Label()
        Me.Label11 = New System.Windows.Forms.Label()
        Me.Label17 = New System.Windows.Forms.Label()
        Me.Label10 = New System.Windows.Forms.Label()
        Me.Label9 = New System.Windows.Forms.Label()
        Me.Label8 = New System.Windows.Forms.Label()
        Me.pnlProbeImage = New System.Windows.Forms.Panel()
        Me.tabViewer = New System.Windows.Forms.TabPage()
        Me.pnlPreviewIntensityProps = New System.Windows.Forms.Panel()
        Me.chkExportVideoIntensity2 = New System.Windows.Forms.CheckBox()
        Me.Label64 = New System.Windows.Forms.Label()
        Me.numBrightnessIntensityOverlay = New System.Windows.Forms.NumericUpDown()
        Me.numIntensityScaling2 = New System.Windows.Forms.NumericUpDown()
        Me.chkDiplayOverlayInIntensity = New System.Windows.Forms.CheckBox()
        Me.Label66 = New System.Windows.Forms.Label()
        Me.Panel3 = New System.Windows.Forms.Panel()
        Me.Label63 = New System.Windows.Forms.Label()
        Me.chkExportVideoField2 = New System.Windows.Forms.CheckBox()
        Me.numFieldScaling2 = New System.Windows.Forms.NumericUpDown()
        Me.Label65 = New System.Windows.Forms.Label()
        Me.numBrightnessFieldOverlay = New System.Windows.Forms.NumericUpDown()
        Me.chkDiplayOverlayInField = New System.Windows.Forms.CheckBox()
        Me.Label56 = New System.Windows.Forms.Label()
        Me.Label55 = New System.Windows.Forms.Label()
        Me.lblWarningIntensity = New System.Windows.Forms.Label()
        Me.lblWarningField = New System.Windows.Forms.Label()
        Me.btnExportIntensityFrame = New System.Windows.Forms.Button()
        Me.btnExportFieldFrame = New System.Windows.Forms.Button()
        Me.btnOpenImageFolder = New System.Windows.Forms.Button()
        Me.btnOpenVideoFolder = New System.Windows.Forms.Button()
        Me.btnViewIntensityVideo = New System.Windows.Forms.Button()
        Me.btnViewFieldVideo = New System.Windows.Forms.Button()
        Me.pnlPreviewIntensity = New System.Windows.Forms.Panel()
        Me.pnlPreviewField = New System.Windows.Forms.Panel()
        Me.tabOutput = New System.Windows.Forms.TabPage()
        Me.btnRenameVideo = New System.Windows.Forms.Button()
        Me.btnShareWithEditors = New System.Windows.Forms.Button()
        Me.btnCopyToClipboard = New System.Windows.Forms.Button()
        Me.btnOpenImageFolder2 = New System.Windows.Forms.Button()
        Me.btnViewVIdeoInPlayer2 = New System.Windows.Forms.Button()
        Me.btnOpenVIdeoFolder2 = New System.Windows.Forms.Button()
        Me.lblPreview = New System.Windows.Forms.Label()
        Me.btnDeleteVideos = New System.Windows.Forms.Button()
        Me.btnDeleteImages = New System.Windows.Forms.Button()
        Me.btnDeleteScene = New System.Windows.Forms.Button()
        Me.btnLoadExternal = New System.Windows.Forms.Button()
        Me.pnlPreview = New System.Windows.Forms.Panel()
        Me.Label57 = New System.Windows.Forms.Label()
        Me.lstScenes = New System.Windows.Forms.ListView()
        Me.colSceneName = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.colSceneCreated = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.lstImages = New System.Windows.Forms.ListView()
        Me.colImageName = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.colCreatedOn = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.lstVideo = New System.Windows.Forms.ListView()
        Me.colVideoName = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.colCreatedOn2 = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.lstAllProjects3 = New System.Windows.Forms.ListView()
        Me.colProjectName2 = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.colLastEdited2 = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.tabColorScheme = New System.Windows.Forms.TabPage()
        Me.lblType = New System.Windows.Forms.Label()
        Me.radSchemeTypeIntensity = New System.Windows.Forms.RadioButton()
        Me.radSchemeTypeField = New System.Windows.Forms.RadioButton()
        Me.btnLoadScheme = New System.Windows.Forms.Button()
        Me.btbChooseFFolder = New System.Windows.Forms.Button()
        Me.lblSchemeFolder = New System.Windows.Forms.Label()
        Me.ListView1 = New System.Windows.Forms.ListView()
        Me.txtSchemeFolder = New System.Windows.Forms.TextBox()
        Me.txtCurrentColorScheme = New System.Windows.Forms.TextBox()
        Me.lblCurrentCOlorScheme = New System.Windows.Forms.Label()
        Me.lblColorSchemeEditor = New System.Windows.Forms.Label()
        Me.btnColor9 = New System.Windows.Forms.Button()
        Me.btnColor8 = New System.Windows.Forms.Button()
        Me.btnColor7 = New System.Windows.Forms.Button()
        Me.btnColor6 = New System.Windows.Forms.Button()
        Me.btnColor5 = New System.Windows.Forms.Button()
        Me.btnColor4 = New System.Windows.Forms.Button()
        Me.btnColor3 = New System.Windows.Forms.Button()
        Me.btnCOlor2 = New System.Windows.Forms.Button()
        Me.btnColor1 = New System.Windows.Forms.Button()
        Me.lblColorPicker = New System.Windows.Forms.Label()
        Me.Panel2 = New System.Windows.Forms.Panel()
        Me.pnlScheme = New System.Windows.Forms.Panel()
        Me.btnDeleteColorScheme = New System.Windows.Forms.Button()
        Me.btnRenameColorScheme = New System.Windows.Forms.Button()
        Me.btnSaveAsColorScheme = New System.Windows.Forms.Button()
        Me.btnSaveCurrentScheme = New System.Windows.Forms.Button()
        Me.btnSaveConfiguration = New System.Windows.Forms.Button()
        Me.btnStartSimulation = New System.Windows.Forms.Button()
        Me.btnPauzeResume = New System.Windows.Forms.Button()
        Me.MenuStrip1 = New System.Windows.Forms.MenuStrip()
        Me.itemFile = New System.Windows.Forms.ToolStripMenuItem()
        Me.NewToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.OpenToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ChooseProjectRootToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.SetCValueToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.SaveToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ExitToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.InfoToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.HelpToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.AboutToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.lblProjectName = New System.Windows.Forms.Label()
        Me.txtProjectName = New System.Windows.Forms.TextBox()
        Me.txtRootFolder = New System.Windows.Forms.TextBox()
        Me.lblRootFolder = New System.Windows.Forms.Label()
        Me.btnBrowseRoot = New System.Windows.Forms.Button()
        Me.StatusStrip1 = New System.Windows.Forms.StatusStrip()
        Me.ToolStripStatusLabel1 = New System.Windows.Forms.ToolStripStatusLabel()
        Me.grpNavigateTabs = New System.Windows.Forms.GroupBox()
        Me.btnGotoPreview = New System.Windows.Forms.Button()
        Me.btnGotoProbes = New System.Windows.Forms.Button()
        Me.btnGotoSources = New System.Windows.Forms.Button()
        Me.btnGotoConfiguration = New System.Windows.Forms.Button()
        Me.grpSimulationControlButtons = New System.Windows.Forms.GroupBox()
        Me.Panel1 = New System.Windows.Forms.Panel()
        Me.btnDescription = New System.Windows.Forms.Button()
        Me.grpShareImage = New System.Windows.Forms.GroupBox()
        Me.btnShareIntensityImage = New System.Windows.Forms.Button()
        Me.btnShareFieldImage = New System.Windows.Forms.Button()
        Me.btnShareSceneImage = New System.Windows.Forms.Button()
        Me.tabMainFunctions.SuspendLayout()
        Me.tabConfiguration.SuspendLayout()
        Me.grpProjects.SuspendLayout()
        Me.grpSimulationSettings.SuspendLayout()
        CType(Me.numTimeDecayFactor, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numSimulationSteps, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numQuality, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numIntensityScaling, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numOffsetIntensity, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numOffsetField, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numFieldScaling, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numVisualizeEvery, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpSceneProperties.SuspendLayout()
        CType(Me.numRefractiveIndex, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.picImagePreview, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numScaleImageSource, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.tabSourceList.SuspendLayout()
        CType(Me.picPreviewSource, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpPlaceSource.SuspendLayout()
        Me.pnlPosition.SuspendLayout()
        CType(Me.numSourcePositionX, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numSourcePositionY, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpSourceDetails.SuspendLayout()
        Me.grpComposite.SuspendLayout()
        CType(Me.numRandomizeOffWidth, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numRandomizePosition, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.NumRandomizePhase, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numRandomizeFrequency, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numSpacingSizeY, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numSpacingSizeX, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numArraySizeY, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numArraySizeX, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpSingleSource.SuspendLayout()
        CType(Me.numOffStateCycles, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numPhase, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numAmplitude, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numFrequency, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numPulseWidthCycles, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.tabProbeList.SuspendLayout()
        Me.GroupBox2.SuspendLayout()
        Me.grpProbe2.SuspendLayout()
        CType(Me.numRunningWindow2, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numBPosY2, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numAPosY2, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numBPosX2, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numAPosX2, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpProbe1.SuspendLayout()
        CType(Me.numRunningWindow, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numBPosY, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numAPosY, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numBPosX, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numAPosX, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.tabViewer.SuspendLayout()
        Me.pnlPreviewIntensityProps.SuspendLayout()
        CType(Me.numBrightnessIntensityOverlay, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numIntensityScaling2, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.Panel3.SuspendLayout()
        CType(Me.numFieldScaling2, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numBrightnessFieldOverlay, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.tabOutput.SuspendLayout()
        Me.tabColorScheme.SuspendLayout()
        Me.MenuStrip1.SuspendLayout()
        Me.StatusStrip1.SuspendLayout()
        Me.grpNavigateTabs.SuspendLayout()
        Me.grpSimulationControlButtons.SuspendLayout()
        Me.Panel1.SuspendLayout()
        Me.grpShareImage.SuspendLayout()
        Me.SuspendLayout()
        '
        'tabMainFunctions
        '
        Me.tabMainFunctions.Controls.Add(Me.tabConfiguration)
        Me.tabMainFunctions.Controls.Add(Me.tabSourceList)
        Me.tabMainFunctions.Controls.Add(Me.tabProbeList)
        Me.tabMainFunctions.Controls.Add(Me.tabViewer)
        Me.tabMainFunctions.Controls.Add(Me.tabOutput)
        Me.tabMainFunctions.Controls.Add(Me.tabColorScheme)
        Me.tabMainFunctions.Font = New System.Drawing.Font("Microsoft Sans Serif", 15.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.tabMainFunctions.Location = New System.Drawing.Point(16, 89)
        Me.tabMainFunctions.Name = "tabMainFunctions"
        Me.tabMainFunctions.Padding = New System.Drawing.Point(10, 3)
        Me.tabMainFunctions.SelectedIndex = 0
        Me.tabMainFunctions.Size = New System.Drawing.Size(1750, 731)
        Me.tabMainFunctions.TabIndex = 0
        '
        'tabConfiguration
        '
        Me.tabConfiguration.Controls.Add(Me.grpProjects)
        Me.tabConfiguration.Controls.Add(Me.grpSimulationSettings)
        Me.tabConfiguration.Controls.Add(Me.grpSceneProperties)
        Me.tabConfiguration.Location = New System.Drawing.Point(4, 34)
        Me.tabConfiguration.Name = "tabConfiguration"
        Me.tabConfiguration.Padding = New System.Windows.Forms.Padding(3)
        Me.tabConfiguration.Size = New System.Drawing.Size(1742, 693)
        Me.tabConfiguration.TabIndex = 1
        Me.tabConfiguration.Text = "     Settings     "
        Me.tabConfiguration.UseVisualStyleBackColor = True
        '
        'grpProjects
        '
        Me.grpProjects.Controls.Add(Me.btnPickRoot)
        Me.grpProjects.Controls.Add(Me.btnSaveConfiguration2)
        Me.grpProjects.Controls.Add(Me.lstAllProjects)
        Me.grpProjects.Controls.Add(Me.btnNewProject)
        Me.grpProjects.Controls.Add(Me.btnRenameProject)
        Me.grpProjects.Controls.Add(Me.btnLoadProject)
        Me.grpProjects.Controls.Add(Me.btnDeleteProject)
        Me.grpProjects.Controls.Add(Me.btnSaveAs)
        Me.grpProjects.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpProjects.Location = New System.Drawing.Point(23, 25)
        Me.grpProjects.Name = "grpProjects"
        Me.grpProjects.Size = New System.Drawing.Size(473, 641)
        Me.grpProjects.TabIndex = 8
        Me.grpProjects.TabStop = False
        Me.grpProjects.Text = "Projects in current root"
        '
        'btnPickRoot
        '
        Me.btnPickRoot.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnPickRoot.Location = New System.Drawing.Point(363, 506)
        Me.btnPickRoot.Name = "btnPickRoot"
        Me.btnPickRoot.Size = New System.Drawing.Size(88, 27)
        Me.btnPickRoot.TabIndex = 10
        Me.btnPickRoot.Text = "Pick root"
        Me.btnPickRoot.UseVisualStyleBackColor = True
        '
        'btnSaveConfiguration2
        '
        Me.btnSaveConfiguration2.Font = New System.Drawing.Font("Microsoft Sans Serif", 15.75!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnSaveConfiguration2.Location = New System.Drawing.Point(252, 562)
        Me.btnSaveConfiguration2.Name = "btnSaveConfiguration2"
        Me.btnSaveConfiguration2.Size = New System.Drawing.Size(191, 49)
        Me.btnSaveConfiguration2.TabIndex = 9
        Me.btnSaveConfiguration2.Text = "Save"
        Me.btnSaveConfiguration2.UseVisualStyleBackColor = True
        '
        'lstAllProjects
        '
        Me.lstAllProjects.BackColor = System.Drawing.SystemColors.MenuBar
        Me.lstAllProjects.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.colProjectName, Me.colLastEdit})
        Me.lstAllProjects.FullRowSelect = True
        Me.lstAllProjects.HideSelection = False
        Me.lstAllProjects.Location = New System.Drawing.Point(16, 28)
        Me.lstAllProjects.MultiSelect = False
        Me.lstAllProjects.Name = "lstAllProjects"
        Me.lstAllProjects.Size = New System.Drawing.Size(435, 472)
        Me.lstAllProjects.TabIndex = 8
        Me.lstAllProjects.UseCompatibleStateImageBehavior = False
        Me.lstAllProjects.View = System.Windows.Forms.View.Details
        '
        'colProjectName
        '
        Me.colProjectName.Text = "ProjectName"
        Me.colProjectName.Width = 280
        '
        'colLastEdit
        '
        Me.colLastEdit.Text = "Last edited"
        Me.colLastEdit.Width = 150
        '
        'btnNewProject
        '
        Me.btnNewProject.Font = New System.Drawing.Font("Microsoft Sans Serif", 15.75!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnNewProject.Location = New System.Drawing.Point(16, 560)
        Me.btnNewProject.Name = "btnNewProject"
        Me.btnNewProject.Size = New System.Drawing.Size(191, 51)
        Me.btnNewProject.TabIndex = 7
        Me.btnNewProject.Text = "New project"
        Me.btnNewProject.UseVisualStyleBackColor = True
        '
        'btnRenameProject
        '
        Me.btnRenameProject.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnRenameProject.Location = New System.Drawing.Point(188, 505)
        Me.btnRenameProject.Name = "btnRenameProject"
        Me.btnRenameProject.Size = New System.Drawing.Size(78, 28)
        Me.btnRenameProject.TabIndex = 6
        Me.btnRenameProject.Text = "Rename"
        Me.btnRenameProject.UseVisualStyleBackColor = True
        '
        'btnLoadProject
        '
        Me.btnLoadProject.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnLoadProject.Location = New System.Drawing.Point(16, 507)
        Me.btnLoadProject.Name = "btnLoadProject"
        Me.btnLoadProject.Size = New System.Drawing.Size(77, 29)
        Me.btnLoadProject.TabIndex = 6
        Me.btnLoadProject.Text = "Load"
        Me.btnLoadProject.UseVisualStyleBackColor = True
        '
        'btnDeleteProject
        '
        Me.btnDeleteProject.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnDeleteProject.Location = New System.Drawing.Point(272, 506)
        Me.btnDeleteProject.Name = "btnDeleteProject"
        Me.btnDeleteProject.Size = New System.Drawing.Size(73, 27)
        Me.btnDeleteProject.TabIndex = 6
        Me.btnDeleteProject.Text = "Delete"
        Me.btnDeleteProject.UseVisualStyleBackColor = True
        '
        'btnSaveAs
        '
        Me.btnSaveAs.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnSaveAs.Location = New System.Drawing.Point(99, 507)
        Me.btnSaveAs.Name = "btnSaveAs"
        Me.btnSaveAs.Size = New System.Drawing.Size(83, 27)
        Me.btnSaveAs.TabIndex = 4
        Me.btnSaveAs.Text = "Save as"
        Me.btnSaveAs.UseVisualStyleBackColor = True
        '
        'grpSimulationSettings
        '
        Me.grpSimulationSettings.Controls.Add(Me.Label61)
        Me.grpSimulationSettings.Controls.Add(Me.numTimeDecayFactor)
        Me.grpSimulationSettings.Controls.Add(Me.numSimulationSteps)
        Me.grpSimulationSettings.Controls.Add(Me.lblInfo)
        Me.grpSimulationSettings.Controls.Add(Me.chkUse4thOrderLaplacian)
        Me.grpSimulationSettings.Controls.Add(Me.chkUseGPU)
        Me.grpSimulationSettings.Controls.Add(Me.numQuality)
        Me.grpSimulationSettings.Controls.Add(Me.lblVideoQuality)
        Me.grpSimulationSettings.Controls.Add(Me.numIntensityScaling)
        Me.grpSimulationSettings.Controls.Add(Me.numOffsetIntensity)
        Me.grpSimulationSettings.Controls.Add(Me.numOffsetField)
        Me.grpSimulationSettings.Controls.Add(Me.numFieldScaling)
        Me.grpSimulationSettings.Controls.Add(Me.Label3)
        Me.grpSimulationSettings.Controls.Add(Me.numScaleIntensityValues)
        Me.grpSimulationSettings.Controls.Add(Me.Label6)
        Me.grpSimulationSettings.Controls.Add(Me.lblOffsetField)
        Me.grpSimulationSettings.Controls.Add(Me.Label5)
        Me.grpSimulationSettings.Controls.Add(Me.Label2)
        Me.grpSimulationSettings.Controls.Add(Me.cmbIntensityColorScheme)
        Me.grpSimulationSettings.Controls.Add(Me.cmbFieldColorScheme)
        Me.grpSimulationSettings.Controls.Add(Me.chkPreviewVideoIntensity)
        Me.grpSimulationSettings.Controls.Add(Me.chkExportVideoIntensity)
        Me.grpSimulationSettings.Controls.Add(Me.chkPreviewVideoField)
        Me.grpSimulationSettings.Controls.Add(Me.chkExportVideoField)
        Me.grpSimulationSettings.Controls.Add(Me.lblPreviewVideo)
        Me.grpSimulationSettings.Controls.Add(Me.lblExportVideo)
        Me.grpSimulationSettings.Controls.Add(Me.numVisualizeEvery)
        Me.grpSimulationSettings.Controls.Add(Me.Label4)
        Me.grpSimulationSettings.Controls.Add(Me.lblVisualizeEvery)
        Me.grpSimulationSettings.Controls.Add(Me.Label7)
        Me.grpSimulationSettings.Controls.Add(Me.lblSimulationSteps)
        Me.grpSimulationSettings.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpSimulationSettings.Location = New System.Drawing.Point(516, 432)
        Me.grpSimulationSettings.Name = "grpSimulationSettings"
        Me.grpSimulationSettings.Size = New System.Drawing.Size(1200, 234)
        Me.grpSimulationSettings.TabIndex = 3
        Me.grpSimulationSettings.TabStop = False
        Me.grpSimulationSettings.Text = "Simulation Settings"
        '
        'Label61
        '
        Me.Label61.AutoSize = True
        Me.Label61.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label61.Location = New System.Drawing.Point(27, 152)
        Me.Label61.Name = "Label61"
        Me.Label61.Size = New System.Drawing.Size(133, 16)
        Me.Label61.TabIndex = 16
        Me.Label61.Text = "Intensity decay factor"
        '
        'numTimeDecayFactor
        '
        Me.numTimeDecayFactor.Location = New System.Drawing.Point(179, 150)
        Me.numTimeDecayFactor.Maximum = New Decimal(New Integer() {20, 0, 0, 0})
        Me.numTimeDecayFactor.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numTimeDecayFactor.Name = "numTimeDecayFactor"
        Me.numTimeDecayFactor.Size = New System.Drawing.Size(80, 24)
        Me.numTimeDecayFactor.TabIndex = 15
        Me.numTimeDecayFactor.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numTimeDecayFactor.Value = New Decimal(New Integer() {5, 0, 0, 0})
        '
        'numSimulationSteps
        '
        Me.numSimulationSteps.Increment = New Decimal(New Integer() {500, 0, 0, 0})
        Me.numSimulationSteps.Location = New System.Drawing.Point(179, 188)
        Me.numSimulationSteps.Maximum = New Decimal(New Integer() {100000, 0, 0, 0})
        Me.numSimulationSteps.Minimum = New Decimal(New Integer() {100, 0, 0, 0})
        Me.numSimulationSteps.Name = "numSimulationSteps"
        Me.numSimulationSteps.Size = New System.Drawing.Size(80, 24)
        Me.numSimulationSteps.TabIndex = 14
        Me.numSimulationSteps.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numSimulationSteps.Value = New Decimal(New Integer() {3000, 0, 0, 0})
        '
        'lblInfo
        '
        Me.lblInfo.AutoSize = True
        Me.lblInfo.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblInfo.Location = New System.Drawing.Point(1051, 193)
        Me.lblInfo.Name = "lblInfo"
        Me.lblInfo.Size = New System.Drawing.Size(37, 16)
        Me.lblInfo.TabIndex = 13
        Me.lblInfo.Text = "(Info)"
        '
        'chkUse4thOrderLaplacian
        '
        Me.chkUse4thOrderLaplacian.AutoSize = True
        Me.chkUse4thOrderLaplacian.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkUse4thOrderLaplacian.Location = New System.Drawing.Point(786, 191)
        Me.chkUse4thOrderLaplacian.Name = "chkUse4thOrderLaplacian"
        Me.chkUse4thOrderLaplacian.Size = New System.Drawing.Size(266, 20)
        Me.chkUse4thOrderLaplacian.TabIndex = 12
        Me.chkUse4thOrderLaplacian.Text = "Use 4th order Laplacian + sub-sampling "
        Me.chkUse4thOrderLaplacian.UseVisualStyleBackColor = True
        '
        'chkUseGPU
        '
        Me.chkUseGPU.AutoSize = True
        Me.chkUseGPU.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkUseGPU.Location = New System.Drawing.Point(787, 153)
        Me.chkUseGPU.Name = "chkUseGPU"
        Me.chkUseGPU.Size = New System.Drawing.Size(187, 20)
        Me.chkUseGPU.TabIndex = 11
        Me.chkUseGPU.Text = "Use GPU (generally faster)"
        Me.chkUseGPU.UseVisualStyleBackColor = True
        '
        'numQuality
        '
        Me.numQuality.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numQuality.Location = New System.Drawing.Point(491, 191)
        Me.numQuality.Name = "numQuality"
        Me.numQuality.Size = New System.Drawing.Size(61, 22)
        Me.numQuality.TabIndex = 10
        Me.numQuality.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numQuality.Value = New Decimal(New Integer() {70, 0, 0, 0})
        '
        'lblVideoQuality
        '
        Me.lblVideoQuality.AutoSize = True
        Me.lblVideoQuality.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblVideoQuality.Location = New System.Drawing.Point(377, 193)
        Me.lblVideoQuality.Name = "lblVideoQuality"
        Me.lblVideoQuality.Size = New System.Drawing.Size(109, 16)
        Me.lblVideoQuality.TabIndex = 9
        Me.lblVideoQuality.Text = "Video quality (%)"
        '
        'numIntensityScaling
        '
        Me.numIntensityScaling.DecimalPlaces = 1
        Me.numIntensityScaling.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numIntensityScaling.Increment = New Decimal(New Integer() {1, 0, 0, 65536})
        Me.numIntensityScaling.Location = New System.Drawing.Point(904, 79)
        Me.numIntensityScaling.Name = "numIntensityScaling"
        Me.numIntensityScaling.Size = New System.Drawing.Size(74, 22)
        Me.numIntensityScaling.TabIndex = 8
        Me.numIntensityScaling.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numIntensityScaling.Value = New Decimal(New Integer() {30, 0, 0, 0})
        '
        'numOffsetIntensity
        '
        Me.numOffsetIntensity.DecimalPlaces = 1
        Me.numOffsetIntensity.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numOffsetIntensity.Increment = New Decimal(New Integer() {1, 0, 0, 131072})
        Me.numOffsetIntensity.Location = New System.Drawing.Point(1044, 80)
        Me.numOffsetIntensity.Maximum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numOffsetIntensity.Minimum = New Decimal(New Integer() {1, 0, 0, -2147483648})
        Me.numOffsetIntensity.Name = "numOffsetIntensity"
        Me.numOffsetIntensity.Size = New System.Drawing.Size(82, 22)
        Me.numOffsetIntensity.TabIndex = 8
        Me.numOffsetIntensity.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numOffsetField
        '
        Me.numOffsetField.DecimalPlaces = 1
        Me.numOffsetField.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numOffsetField.Increment = New Decimal(New Integer() {1, 0, 0, 131072})
        Me.numOffsetField.Location = New System.Drawing.Point(1044, 40)
        Me.numOffsetField.Maximum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numOffsetField.Minimum = New Decimal(New Integer() {1, 0, 0, -2147483648})
        Me.numOffsetField.Name = "numOffsetField"
        Me.numOffsetField.Size = New System.Drawing.Size(82, 22)
        Me.numOffsetField.TabIndex = 8
        Me.numOffsetField.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numFieldScaling
        '
        Me.numFieldScaling.DecimalPlaces = 1
        Me.numFieldScaling.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numFieldScaling.Increment = New Decimal(New Integer() {1, 0, 0, 65536})
        Me.numFieldScaling.Location = New System.Drawing.Point(904, 40)
        Me.numFieldScaling.Name = "numFieldScaling"
        Me.numFieldScaling.Size = New System.Drawing.Size(74, 22)
        Me.numFieldScaling.TabIndex = 8
        Me.numFieldScaling.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numFieldScaling.Value = New Decimal(New Integer() {30, 0, 0, 0})
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label3.Location = New System.Drawing.Point(363, 83)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(140, 16)
        Me.Label3.TabIndex = 6
        Me.Label3.Text = "Intensity color scheme"
        '
        'numScaleIntensityValues
        '
        Me.numScaleIntensityValues.AutoSize = True
        Me.numScaleIntensityValues.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numScaleIntensityValues.Location = New System.Drawing.Point(785, 81)
        Me.numScaleIntensityValues.Name = "numScaleIntensityValues"
        Me.numScaleIntensityValues.Size = New System.Drawing.Size(105, 16)
        Me.numScaleIntensityValues.TabIndex = 6
        Me.numScaleIntensityValues.Text = "Scale Int. values"
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label6.Location = New System.Drawing.Point(997, 82)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(42, 16)
        Me.Label6.TabIndex = 6
        Me.Label6.Text = "Offset"
        '
        'lblOffsetField
        '
        Me.lblOffsetField.AutoSize = True
        Me.lblOffsetField.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblOffsetField.Location = New System.Drawing.Point(996, 42)
        Me.lblOffsetField.Name = "lblOffsetField"
        Me.lblOffsetField.Size = New System.Drawing.Size(42, 16)
        Me.lblOffsetField.TabIndex = 6
        Me.lblOffsetField.Text = "Offset"
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label5.Location = New System.Drawing.Point(783, 42)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(114, 16)
        Me.Label5.TabIndex = 6
        Me.Label5.Text = "Scale field values"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label2.Location = New System.Drawing.Point(363, 44)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(122, 16)
        Me.Label2.TabIndex = 6
        Me.Label2.Text = "Field color scheme"
        '
        'cmbIntensityColorScheme
        '
        Me.cmbIntensityColorScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.cmbIntensityColorScheme.FormattingEnabled = True
        Me.cmbIntensityColorScheme.Location = New System.Drawing.Point(509, 79)
        Me.cmbIntensityColorScheme.Name = "cmbIntensityColorScheme"
        Me.cmbIntensityColorScheme.Size = New System.Drawing.Size(165, 24)
        Me.cmbIntensityColorScheme.TabIndex = 5
        '
        'cmbFieldColorScheme
        '
        Me.cmbFieldColorScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.cmbFieldColorScheme.FormattingEnabled = True
        Me.cmbFieldColorScheme.Location = New System.Drawing.Point(509, 40)
        Me.cmbFieldColorScheme.Name = "cmbFieldColorScheme"
        Me.cmbFieldColorScheme.Size = New System.Drawing.Size(165, 24)
        Me.cmbFieldColorScheme.TabIndex = 5
        '
        'chkPreviewVideoIntensity
        '
        Me.chkPreviewVideoIntensity.AutoSize = True
        Me.chkPreviewVideoIntensity.Checked = True
        Me.chkPreviewVideoIntensity.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkPreviewVideoIntensity.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkPreviewVideoIntensity.Location = New System.Drawing.Point(193, 80)
        Me.chkPreviewVideoIntensity.Name = "chkPreviewVideoIntensity"
        Me.chkPreviewVideoIntensity.Size = New System.Drawing.Size(75, 20)
        Me.chkPreviewVideoIntensity.TabIndex = 4
        Me.chkPreviewVideoIntensity.Text = "Intensity"
        Me.chkPreviewVideoIntensity.UseVisualStyleBackColor = True
        '
        'chkExportVideoIntensity
        '
        Me.chkExportVideoIntensity.AutoSize = True
        Me.chkExportVideoIntensity.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkExportVideoIntensity.Location = New System.Drawing.Point(193, 41)
        Me.chkExportVideoIntensity.Name = "chkExportVideoIntensity"
        Me.chkExportVideoIntensity.Size = New System.Drawing.Size(75, 20)
        Me.chkExportVideoIntensity.TabIndex = 4
        Me.chkExportVideoIntensity.Text = "Intensity"
        Me.chkExportVideoIntensity.UseVisualStyleBackColor = True
        '
        'chkPreviewVideoField
        '
        Me.chkPreviewVideoField.AutoSize = True
        Me.chkPreviewVideoField.Checked = True
        Me.chkPreviewVideoField.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkPreviewVideoField.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkPreviewVideoField.Location = New System.Drawing.Point(130, 81)
        Me.chkPreviewVideoField.Name = "chkPreviewVideoField"
        Me.chkPreviewVideoField.Size = New System.Drawing.Size(57, 20)
        Me.chkPreviewVideoField.TabIndex = 4
        Me.chkPreviewVideoField.Text = "Field"
        Me.chkPreviewVideoField.UseVisualStyleBackColor = True
        '
        'chkExportVideoField
        '
        Me.chkExportVideoField.AutoSize = True
        Me.chkExportVideoField.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkExportVideoField.Location = New System.Drawing.Point(130, 42)
        Me.chkExportVideoField.Name = "chkExportVideoField"
        Me.chkExportVideoField.Size = New System.Drawing.Size(57, 20)
        Me.chkExportVideoField.TabIndex = 4
        Me.chkExportVideoField.Text = "Field"
        Me.chkExportVideoField.UseVisualStyleBackColor = True
        '
        'lblPreviewVideo
        '
        Me.lblPreviewVideo.AutoSize = True
        Me.lblPreviewVideo.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblPreviewVideo.Location = New System.Drawing.Point(24, 82)
        Me.lblPreviewVideo.Name = "lblPreviewVideo"
        Me.lblPreviewVideo.Size = New System.Drawing.Size(93, 16)
        Me.lblPreviewVideo.TabIndex = 3
        Me.lblPreviewVideo.Text = "Preview video"
        '
        'lblExportVideo
        '
        Me.lblExportVideo.AutoSize = True
        Me.lblExportVideo.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblExportVideo.Location = New System.Drawing.Point(24, 43)
        Me.lblExportVideo.Name = "lblExportVideo"
        Me.lblExportVideo.Size = New System.Drawing.Size(100, 16)
        Me.lblExportVideo.TabIndex = 3
        Me.lblExportVideo.Text = "Export video of:"
        '
        'numVisualizeEvery
        '
        Me.numVisualizeEvery.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numVisualizeEvery.Location = New System.Drawing.Point(491, 152)
        Me.numVisualizeEvery.Maximum = New Decimal(New Integer() {6, 0, 0, 0})
        Me.numVisualizeEvery.Name = "numVisualizeEvery"
        Me.numVisualizeEvery.Size = New System.Drawing.Size(61, 22)
        Me.numVisualizeEvery.TabIndex = 1
        Me.numVisualizeEvery.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numVisualizeEvery.Value = New Decimal(New Integer() {2, 0, 0, 0})
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label4.Location = New System.Drawing.Point(558, 155)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(49, 16)
        Me.Label4.TabIndex = 0
        Me.Label4.Text = "frames"
        '
        'lblVisualizeEvery
        '
        Me.lblVisualizeEvery.AutoSize = True
        Me.lblVisualizeEvery.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblVisualizeEvery.Location = New System.Drawing.Point(377, 154)
        Me.lblVisualizeEvery.Name = "lblVisualizeEvery"
        Me.lblVisualizeEvery.Size = New System.Drawing.Size(99, 16)
        Me.lblVisualizeEvery.TabIndex = 0
        Me.lblVisualizeEvery.Text = "Visualize every"
        '
        'Label7
        '
        Me.Label7.AutoSize = True
        Me.Label7.Location = New System.Drawing.Point(386, 195)
        Me.Label7.Name = "Label7"
        Me.Label7.Size = New System.Drawing.Size(0, 18)
        Me.Label7.TabIndex = 0
        '
        'lblSimulationSteps
        '
        Me.lblSimulationSteps.AutoSize = True
        Me.lblSimulationSteps.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblSimulationSteps.Location = New System.Drawing.Point(32, 193)
        Me.lblSimulationSteps.Name = "lblSimulationSteps"
        Me.lblSimulationSteps.Size = New System.Drawing.Size(106, 16)
        Me.lblSimulationSteps.TabIndex = 0
        Me.lblSimulationSteps.Text = "Simulation steps"
        '
        'grpSceneProperties
        '
        Me.grpSceneProperties.Controls.Add(Me.Label20)
        Me.grpSceneProperties.Controls.Add(Me.numRefractiveIndex)
        Me.grpSceneProperties.Controls.Add(Me.picImagePreview)
        Me.grpSceneProperties.Controls.Add(Me.lblScaleImgSourceIntensity)
        Me.grpSceneProperties.Controls.Add(Me.numScaleImageSource)
        Me.grpSceneProperties.Controls.Add(Me.btnEditObjectSourceList)
        Me.grpSceneProperties.Controls.Add(Me.txtDampingEdgeSize)
        Me.grpSceneProperties.Controls.Add(Me.chkUseObjectSources)
        Me.grpSceneProperties.Controls.Add(Me.chkUseImageSources)
        Me.grpSceneProperties.Controls.Add(Me.chkDampingEdge)
        Me.grpSceneProperties.Controls.Add(Me.btnLoadSceneImage)
        Me.grpSceneProperties.Controls.Add(Me.Label1)
        Me.grpSceneProperties.Controls.Add(Me.lblPix)
        Me.grpSceneProperties.Controls.Add(Me.lblX)
        Me.grpSceneProperties.Controls.Add(Me.txtYsize)
        Me.grpSceneProperties.Controls.Add(Me.txtXSize)
        Me.grpSceneProperties.Controls.Add(Me.radUserDefinedScene)
        Me.grpSceneProperties.Controls.Add(Me.radUseSceneImage)
        Me.grpSceneProperties.Controls.Add(Me.lblImageNameAndSize)
        Me.grpSceneProperties.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpSceneProperties.Location = New System.Drawing.Point(516, 25)
        Me.grpSceneProperties.Name = "grpSceneProperties"
        Me.grpSceneProperties.Size = New System.Drawing.Size(1200, 390)
        Me.grpSceneProperties.TabIndex = 1
        Me.grpSceneProperties.TabStop = False
        Me.grpSceneProperties.Text = "Scene properties"
        '
        'Label20
        '
        Me.Label20.AutoSize = True
        Me.Label20.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label20.Location = New System.Drawing.Point(366, 80)
        Me.Label20.Name = "Label20"
        Me.Label20.Size = New System.Drawing.Size(104, 16)
        Me.Label20.TabIndex = 12
        Me.Label20.Text = "Refractive index"
        '
        'numRefractiveIndex
        '
        Me.numRefractiveIndex.DecimalPlaces = 2
        Me.numRefractiveIndex.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numRefractiveIndex.Increment = New Decimal(New Integer() {1, 0, 0, 131072})
        Me.numRefractiveIndex.Location = New System.Drawing.Point(476, 78)
        Me.numRefractiveIndex.Maximum = New Decimal(New Integer() {25, 0, 0, 65536})
        Me.numRefractiveIndex.Minimum = New Decimal(New Integer() {10, 0, 0, 65536})
        Me.numRefractiveIndex.Name = "numRefractiveIndex"
        Me.numRefractiveIndex.Size = New System.Drawing.Size(60, 22)
        Me.numRefractiveIndex.TabIndex = 11
        Me.numRefractiveIndex.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numRefractiveIndex.Value = New Decimal(New Integer() {10, 0, 0, 65536})
        '
        'picImagePreview
        '
        Me.picImagePreview.BackColor = System.Drawing.SystemColors.Control
        Me.picImagePreview.Location = New System.Drawing.Point(587, 22)
        Me.picImagePreview.Name = "picImagePreview"
        Me.picImagePreview.Size = New System.Drawing.Size(600, 338)
        Me.picImagePreview.TabIndex = 1
        Me.picImagePreview.TabStop = False
        '
        'lblScaleImgSourceIntensity
        '
        Me.lblScaleImgSourceIntensity.AutoSize = True
        Me.lblScaleImgSourceIntensity.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblScaleImgSourceIntensity.Location = New System.Drawing.Point(179, 183)
        Me.lblScaleImgSourceIntensity.Name = "lblScaleImgSourceIntensity"
        Me.lblScaleImgSourceIntensity.Size = New System.Drawing.Size(155, 16)
        Me.lblScaleImgSourceIntensity.TabIndex = 10
        Me.lblScaleImgSourceIntensity.Text = "Scale intensity values by"
        '
        'numScaleImageSource
        '
        Me.numScaleImageSource.DecimalPlaces = 1
        Me.numScaleImageSource.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numScaleImageSource.Increment = New Decimal(New Integer() {1, 0, 0, 65536})
        Me.numScaleImageSource.Location = New System.Drawing.Point(337, 181)
        Me.numScaleImageSource.Name = "numScaleImageSource"
        Me.numScaleImageSource.Size = New System.Drawing.Size(65, 22)
        Me.numScaleImageSource.TabIndex = 9
        Me.numScaleImageSource.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numScaleImageSource.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'btnEditObjectSourceList
        '
        Me.btnEditObjectSourceList.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnEditObjectSourceList.Location = New System.Drawing.Point(201, 214)
        Me.btnEditObjectSourceList.Name = "btnEditObjectSourceList"
        Me.btnEditObjectSourceList.Size = New System.Drawing.Size(117, 23)
        Me.btnEditObjectSourceList.TabIndex = 8
        Me.btnEditObjectSourceList.Text = "Edit source list"
        Me.btnEditObjectSourceList.UseVisualStyleBackColor = True
        '
        'txtDampingEdgeSize
        '
        Me.txtDampingEdgeSize.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.txtDampingEdgeSize.Location = New System.Drawing.Point(152, 143)
        Me.txtDampingEdgeSize.Name = "txtDampingEdgeSize"
        Me.txtDampingEdgeSize.Size = New System.Drawing.Size(100, 22)
        Me.txtDampingEdgeSize.TabIndex = 7
        '
        'chkUseObjectSources
        '
        Me.chkUseObjectSources.AutoSize = True
        Me.chkUseObjectSources.Checked = True
        Me.chkUseObjectSources.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkUseObjectSources.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkUseObjectSources.Location = New System.Drawing.Point(26, 218)
        Me.chkUseObjectSources.Name = "chkUseObjectSources"
        Me.chkUseObjectSources.Size = New System.Drawing.Size(143, 20)
        Me.chkUseObjectSources.TabIndex = 6
        Me.chkUseObjectSources.Text = "Use object sources"
        Me.chkUseObjectSources.UseVisualStyleBackColor = True
        '
        'chkUseImageSources
        '
        Me.chkUseImageSources.AutoSize = True
        Me.chkUseImageSources.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkUseImageSources.Location = New System.Drawing.Point(26, 182)
        Me.chkUseImageSources.Name = "chkUseImageSources"
        Me.chkUseImageSources.Size = New System.Drawing.Size(157, 20)
        Me.chkUseImageSources.TabIndex = 6
        Me.chkUseImageSources.Text = "Use image sources   -"
        Me.chkUseImageSources.UseVisualStyleBackColor = True
        '
        'chkDampingEdge
        '
        Me.chkDampingEdge.AutoSize = True
        Me.chkDampingEdge.Checked = True
        Me.chkDampingEdge.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkDampingEdge.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkDampingEdge.Location = New System.Drawing.Point(26, 145)
        Me.chkDampingEdge.Name = "chkDampingEdge"
        Me.chkDampingEdge.Size = New System.Drawing.Size(120, 20)
        Me.chkDampingEdge.TabIndex = 6
        Me.chkDampingEdge.Text = "Damping edge "
        Me.chkDampingEdge.UseVisualStyleBackColor = True
        '
        'btnLoadSceneImage
        '
        Me.btnLoadSceneImage.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnLoadSceneImage.Location = New System.Drawing.Point(138, 39)
        Me.btnLoadSceneImage.Name = "btnLoadSceneImage"
        Me.btnLoadSceneImage.Size = New System.Drawing.Size(75, 23)
        Me.btnLoadSceneImage.TabIndex = 5
        Me.btnLoadSceneImage.Text = "load"
        Me.btnLoadSceneImage.UseVisualStyleBackColor = True
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label1.Location = New System.Drawing.Point(258, 146)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(22, 16)
        Me.Label1.TabIndex = 4
        Me.Label1.Text = "px"
        '
        'lblPix
        '
        Me.lblPix.AutoSize = True
        Me.lblPix.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblPix.Location = New System.Drawing.Point(327, 80)
        Me.lblPix.Name = "lblPix"
        Me.lblPix.Size = New System.Drawing.Size(22, 16)
        Me.lblPix.TabIndex = 4
        Me.lblPix.Text = "px"
        '
        'lblX
        '
        Me.lblX.AutoSize = True
        Me.lblX.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblX.Location = New System.Drawing.Point(243, 80)
        Me.lblX.Name = "lblX"
        Me.lblX.Size = New System.Drawing.Size(12, 16)
        Me.lblX.TabIndex = 4
        Me.lblX.Text = "/"
        '
        'txtYsize
        '
        Me.txtYsize.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.txtYsize.Location = New System.Drawing.Point(258, 77)
        Me.txtYsize.Name = "txtYsize"
        Me.txtYsize.Size = New System.Drawing.Size(64, 22)
        Me.txtYsize.TabIndex = 3
        '
        'txtXSize
        '
        Me.txtXSize.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.txtXSize.Location = New System.Drawing.Point(169, 76)
        Me.txtXSize.Name = "txtXSize"
        Me.txtXSize.Size = New System.Drawing.Size(72, 22)
        Me.txtXSize.TabIndex = 3
        '
        'radUserDefinedScene
        '
        Me.radUserDefinedScene.AutoSize = True
        Me.radUserDefinedScene.Checked = True
        Me.radUserDefinedScene.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.radUserDefinedScene.Location = New System.Drawing.Point(26, 77)
        Me.radUserDefinedScene.Name = "radUserDefinedScene"
        Me.radUserDefinedScene.Size = New System.Drawing.Size(141, 20)
        Me.radUserDefinedScene.TabIndex = 2
        Me.radUserDefinedScene.TabStop = True
        Me.radUserDefinedScene.Text = "User defined  (X /Y)"
        Me.radUserDefinedScene.UseVisualStyleBackColor = True
        '
        'radUseSceneImage
        '
        Me.radUseSceneImage.AutoSize = True
        Me.radUseSceneImage.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.radUseSceneImage.Location = New System.Drawing.Point(26, 40)
        Me.radUseSceneImage.Name = "radUseSceneImage"
        Me.radUseSceneImage.Size = New System.Drawing.Size(106, 20)
        Me.radUseSceneImage.TabIndex = 0
        Me.radUseSceneImage.Text = "Scene image"
        Me.radUseSceneImage.UseVisualStyleBackColor = True
        '
        'lblImageNameAndSize
        '
        Me.lblImageNameAndSize.AutoSize = True
        Me.lblImageNameAndSize.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblImageNameAndSize.Location = New System.Drawing.Point(228, 41)
        Me.lblImageNameAndSize.Name = "lblImageNameAndSize"
        Me.lblImageNameAndSize.Size = New System.Drawing.Size(103, 16)
        Me.lblImageNameAndSize.TabIndex = 4
        Me.lblImageNameAndSize.Text = "(name and size)"
        '
        'tabSourceList
        '
        Me.tabSourceList.Controls.Add(Me.Label62)
        Me.tabSourceList.Controls.Add(Me.Label60)
        Me.tabSourceList.Controls.Add(Me.chkAutoRescaleSourceOn)
        Me.tabSourceList.Controls.Add(Me.picPreviewSource)
        Me.tabSourceList.Controls.Add(Me.grpPlaceSource)
        Me.tabSourceList.Controls.Add(Me.btnDelete)
        Me.tabSourceList.Controls.Add(Me.btnNew)
        Me.tabSourceList.Controls.Add(Me.btnCopy)
        Me.tabSourceList.Controls.Add(Me.lstSources)
        Me.tabSourceList.Controls.Add(Me.pnlSourceImage)
        Me.tabSourceList.Controls.Add(Me.grpSourceDetails)
        Me.tabSourceList.Location = New System.Drawing.Point(4, 34)
        Me.tabSourceList.Name = "tabSourceList"
        Me.tabSourceList.Size = New System.Drawing.Size(1742, 693)
        Me.tabSourceList.TabIndex = 6
        Me.tabSourceList.Text = "     Sources     "
        Me.tabSourceList.UseVisualStyleBackColor = True
        '
        'Label62
        '
        Me.Label62.AutoSize = True
        Me.Label62.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label62.Location = New System.Drawing.Point(1187, 18)
        Me.Label62.Name = "Label62"
        Me.Label62.Size = New System.Drawing.Size(121, 18)
        Me.Label62.TabIndex = 8
        Me.Label62.Text = "List of sources"
        '
        'Label60
        '
        Me.Label60.AutoSize = True
        Me.Label60.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label60.Location = New System.Drawing.Point(1185, 408)
        Me.Label60.Name = "Label60"
        Me.Label60.Size = New System.Drawing.Size(124, 18)
        Me.Label60.TabIndex = 7
        Me.Label60.Text = "Source preview"
        '
        'chkAutoRescaleSourceOn
        '
        Me.chkAutoRescaleSourceOn.AutoSize = True
        Me.chkAutoRescaleSourceOn.Checked = True
        Me.chkAutoRescaleSourceOn.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkAutoRescaleSourceOn.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkAutoRescaleSourceOn.Location = New System.Drawing.Point(1192, 651)
        Me.chkAutoRescaleSourceOn.Name = "chkAutoRescaleSourceOn"
        Me.chkAutoRescaleSourceOn.Size = New System.Drawing.Size(186, 20)
        Me.chkAutoRescaleSourceOn.TabIndex = 6
        Me.chkAutoRescaleSourceOn.Text = "Auto-rescale source layout"
        Me.chkAutoRescaleSourceOn.UseVisualStyleBackColor = True
        '
        'picPreviewSource
        '
        Me.picPreviewSource.Location = New System.Drawing.Point(1185, 430)
        Me.picPreviewSource.Name = "picPreviewSource"
        Me.picPreviewSource.Size = New System.Drawing.Size(273, 215)
        Me.picPreviewSource.TabIndex = 5
        Me.picPreviewSource.TabStop = False
        '
        'grpPlaceSource
        '
        Me.grpPlaceSource.Controls.Add(Me.pnlPosition)
        Me.grpPlaceSource.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpPlaceSource.Location = New System.Drawing.Point(1185, 285)
        Me.grpPlaceSource.Name = "grpPlaceSource"
        Me.grpPlaceSource.Size = New System.Drawing.Size(273, 117)
        Me.grpPlaceSource.TabIndex = 4
        Me.grpPlaceSource.TabStop = False
        Me.grpPlaceSource.Text = "Place current source"
        '
        'pnlPosition
        '
        Me.pnlPosition.BackColor = System.Drawing.Color.Azure
        Me.pnlPosition.Controls.Add(Me.numSourcePositionX)
        Me.pnlPosition.Controls.Add(Me.Label36)
        Me.pnlPosition.Controls.Add(Me.lblPosition)
        Me.pnlPosition.Controls.Add(Me.btnPickSourcePosition)
        Me.pnlPosition.Controls.Add(Me.numSourcePositionY)
        Me.pnlPosition.Controls.Add(Me.Label35)
        Me.pnlPosition.Location = New System.Drawing.Point(22, 25)
        Me.pnlPosition.Name = "pnlPosition"
        Me.pnlPosition.Size = New System.Drawing.Size(228, 77)
        Me.pnlPosition.TabIndex = 20
        '
        'numSourcePositionX
        '
        Me.numSourcePositionX.Location = New System.Drawing.Point(146, 9)
        Me.numSourcePositionX.Name = "numSourcePositionX"
        Me.numSourcePositionX.Size = New System.Drawing.Size(65, 22)
        Me.numSourcePositionX.TabIndex = 11
        Me.numSourcePositionX.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'Label36
        '
        Me.Label36.AutoSize = True
        Me.Label36.Location = New System.Drawing.Point(123, 11)
        Me.Label36.Name = "Label36"
        Me.Label36.Size = New System.Drawing.Size(16, 16)
        Me.Label36.TabIndex = 7
        Me.Label36.Text = "X"
        '
        'lblPosition
        '
        Me.lblPosition.AutoSize = True
        Me.lblPosition.Location = New System.Drawing.Point(16, 6)
        Me.lblPosition.Name = "lblPosition"
        Me.lblPosition.Size = New System.Drawing.Size(97, 16)
        Me.lblPosition.TabIndex = 12
        Me.lblPosition.Text = "Center position"
        '
        'btnPickSourcePosition
        '
        Me.btnPickSourcePosition.Font = New System.Drawing.Font("Microsoft Sans Serif", 14.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnPickSourcePosition.Location = New System.Drawing.Point(15, 31)
        Me.btnPickSourcePosition.Name = "btnPickSourcePosition"
        Me.btnPickSourcePosition.Size = New System.Drawing.Size(102, 35)
        Me.btnPickSourcePosition.TabIndex = 9
        Me.btnPickSourcePosition.Text = "Pick "
        Me.btnPickSourcePosition.UseVisualStyleBackColor = True
        '
        'numSourcePositionY
        '
        Me.numSourcePositionY.Location = New System.Drawing.Point(146, 41)
        Me.numSourcePositionY.Name = "numSourcePositionY"
        Me.numSourcePositionY.Size = New System.Drawing.Size(65, 22)
        Me.numSourcePositionY.TabIndex = 10
        Me.numSourcePositionY.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'Label35
        '
        Me.Label35.AutoSize = True
        Me.Label35.Location = New System.Drawing.Point(123, 43)
        Me.Label35.Name = "Label35"
        Me.Label35.Size = New System.Drawing.Size(17, 16)
        Me.Label35.TabIndex = 6
        Me.Label35.Text = "Y"
        '
        'btnDelete
        '
        Me.btnDelete.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnDelete.Location = New System.Drawing.Point(1389, 245)
        Me.btnDelete.Name = "btnDelete"
        Me.btnDelete.Size = New System.Drawing.Size(70, 29)
        Me.btnDelete.TabIndex = 2
        Me.btnDelete.Text = "Delete"
        Me.btnDelete.UseVisualStyleBackColor = True
        '
        'btnNew
        '
        Me.btnNew.BackColor = System.Drawing.Color.Azure
        Me.btnNew.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnNew.Location = New System.Drawing.Point(1189, 245)
        Me.btnNew.Name = "btnNew"
        Me.btnNew.Size = New System.Drawing.Size(70, 28)
        Me.btnNew.TabIndex = 2
        Me.btnNew.Text = "New"
        Me.btnNew.UseVisualStyleBackColor = False
        '
        'btnCopy
        '
        Me.btnCopy.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnCopy.Location = New System.Drawing.Point(1314, 244)
        Me.btnCopy.Name = "btnCopy"
        Me.btnCopy.Size = New System.Drawing.Size(70, 29)
        Me.btnCopy.TabIndex = 2
        Me.btnCopy.Text = "Copy"
        Me.btnCopy.UseVisualStyleBackColor = True
        '
        'lstSources
        '
        Me.lstSources.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lstSources.FormattingEnabled = True
        Me.lstSources.ItemHeight = 16
        Me.lstSources.Location = New System.Drawing.Point(1187, 43)
        Me.lstSources.Name = "lstSources"
        Me.lstSources.Size = New System.Drawing.Size(273, 196)
        Me.lstSources.TabIndex = 1
        '
        'pnlSourceImage
        '
        Me.pnlSourceImage.Location = New System.Drawing.Point(16, 14)
        Me.pnlSourceImage.Name = "pnlSourceImage"
        Me.pnlSourceImage.Size = New System.Drawing.Size(1160, 653)
        Me.pnlSourceImage.TabIndex = 0
        '
        'grpSourceDetails
        '
        Me.grpSourceDetails.Controls.Add(Me.txtDetachAfterSteps)
        Me.grpSourceDetails.Controls.Add(Me.btnSaveSource)
        Me.grpSourceDetails.Controls.Add(Me.Label47)
        Me.grpSourceDetails.Controls.Add(Me.chkDetachSource)
        Me.grpSourceDetails.Controls.Add(Me.lblLambdaIs)
        Me.grpSourceDetails.Controls.Add(Me.chkSourceOn)
        Me.grpSourceDetails.Controls.Add(Me.grpComposite)
        Me.grpSourceDetails.Controls.Add(Me.grpSingleSource)
        Me.grpSourceDetails.Controls.Add(Me.radModePulsed)
        Me.grpSourceDetails.Controls.Add(Me.radModeContinuous)
        Me.grpSourceDetails.Controls.Add(Me.txtName)
        Me.grpSourceDetails.Controls.Add(Me.cmbMainType)
        Me.grpSourceDetails.Controls.Add(Me.Label39)
        Me.grpSourceDetails.Controls.Add(Me.lblName)
        Me.grpSourceDetails.Controls.Add(Me.Label38)
        Me.grpSourceDetails.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpSourceDetails.Location = New System.Drawing.Point(1471, 15)
        Me.grpSourceDetails.Name = "grpSourceDetails"
        Me.grpSourceDetails.Size = New System.Drawing.Size(254, 666)
        Me.grpSourceDetails.TabIndex = 3
        Me.grpSourceDetails.TabStop = False
        Me.grpSourceDetails.Text = "Source               "
        '
        'txtDetachAfterSteps
        '
        Me.txtDetachAfterSteps.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtDetachAfterSteps.Location = New System.Drawing.Point(128, 591)
        Me.txtDetachAfterSteps.Name = "txtDetachAfterSteps"
        Me.txtDetachAfterSteps.Size = New System.Drawing.Size(65, 22)
        Me.txtDetachAfterSteps.TabIndex = 23
        '
        'btnSaveSource
        '
        Me.btnSaveSource.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnSaveSource.Location = New System.Drawing.Point(12, 623)
        Me.btnSaveSource.Name = "btnSaveSource"
        Me.btnSaveSource.Size = New System.Drawing.Size(228, 30)
        Me.btnSaveSource.TabIndex = 17
        Me.btnSaveSource.Text = "Save source data"
        Me.btnSaveSource.UseVisualStyleBackColor = True
        '
        'Label47
        '
        Me.Label47.AutoSize = True
        Me.Label47.Location = New System.Drawing.Point(199, 594)
        Me.Label47.Name = "Label47"
        Me.Label47.Size = New System.Drawing.Size(41, 16)
        Me.Label47.TabIndex = 22
        Me.Label47.Text = "steps"
        '
        'chkDetachSource
        '
        Me.chkDetachSource.AutoSize = True
        Me.chkDetachSource.Location = New System.Drawing.Point(23, 592)
        Me.chkDetachSource.Name = "chkDetachSource"
        Me.chkDetachSource.Size = New System.Drawing.Size(99, 20)
        Me.chkDetachSource.TabIndex = 21
        Me.chkDetachSource.Text = "Detach after"
        Me.chkDetachSource.UseVisualStyleBackColor = True
        '
        'lblLambdaIs
        '
        Me.lblLambdaIs.AutoSize = True
        Me.lblLambdaIs.BackColor = System.Drawing.Color.White
        Me.lblLambdaIs.Location = New System.Drawing.Point(162, 1)
        Me.lblLambdaIs.Name = "lblLambdaIs"
        Me.lblLambdaIs.Size = New System.Drawing.Size(52, 16)
        Me.lblLambdaIs.TabIndex = 19
        Me.lblLambdaIs.Text = "λ=     px"
        '
        'chkSourceOn
        '
        Me.chkSourceOn.AutoSize = True
        Me.chkSourceOn.Location = New System.Drawing.Point(60, 0)
        Me.chkSourceOn.Name = "chkSourceOn"
        Me.chkSourceOn.Size = New System.Drawing.Size(42, 20)
        Me.chkSourceOn.TabIndex = 18
        Me.chkSourceOn.Text = "on"
        Me.chkSourceOn.UseVisualStyleBackColor = True
        '
        'grpComposite
        '
        Me.grpComposite.Controls.Add(Me.Label59)
        Me.grpComposite.Controls.Add(Me.Label58)
        Me.grpComposite.Controls.Add(Me.numRandomizeOffWidth)
        Me.grpComposite.Controls.Add(Me.numRandomizePosition)
        Me.grpComposite.Controls.Add(Me.NumRandomizePhase)
        Me.grpComposite.Controls.Add(Me.numRandomizeFrequency)
        Me.grpComposite.Controls.Add(Me.chkRandomizePhase)
        Me.grpComposite.Controls.Add(Me.chkRandomizePosition)
        Me.grpComposite.Controls.Add(Me.chkOffWidth)
        Me.grpComposite.Controls.Add(Me.chkRandomizeFrequency)
        Me.grpComposite.Controls.Add(Me.numSpacingSizeY)
        Me.grpComposite.Controls.Add(Me.numSpacingSizeX)
        Me.grpComposite.Controls.Add(Me.numArraySizeY)
        Me.grpComposite.Controls.Add(Me.numArraySizeX)
        Me.grpComposite.Controls.Add(Me.Label50)
        Me.grpComposite.Controls.Add(Me.Label51)
        Me.grpComposite.Controls.Add(Me.Label52)
        Me.grpComposite.Controls.Add(Me.Label53)
        Me.grpComposite.Controls.Add(Me.cmbCompositeProperties)
        Me.grpComposite.Location = New System.Drawing.Point(12, 344)
        Me.grpComposite.Name = "grpComposite"
        Me.grpComposite.Size = New System.Drawing.Size(230, 241)
        Me.grpComposite.TabIndex = 16
        Me.grpComposite.TabStop = False
        Me.grpComposite.Text = "Composite source"
        '
        'Label59
        '
        Me.Label59.AutoSize = True
        Me.Label59.Location = New System.Drawing.Point(181, 17)
        Me.Label59.Name = "Label59"
        Me.Label59.Size = New System.Drawing.Size(15, 16)
        Me.Label59.TabIndex = 16
        Me.Label59.Text = "y"
        '
        'Label58
        '
        Me.Label58.AutoSize = True
        Me.Label58.Location = New System.Drawing.Point(110, 17)
        Me.Label58.Name = "Label58"
        Me.Label58.Size = New System.Drawing.Size(14, 16)
        Me.Label58.TabIndex = 16
        Me.Label58.Text = "x"
        '
        'numRandomizeOffWidth
        '
        Me.numRandomizeOffWidth.Location = New System.Drawing.Point(17, 208)
        Me.numRandomizeOffWidth.Maximum = New Decimal(New Integer() {10000, 0, 0, 0})
        Me.numRandomizeOffWidth.Name = "numRandomizeOffWidth"
        Me.numRandomizeOffWidth.Size = New System.Drawing.Size(81, 22)
        Me.numRandomizeOffWidth.TabIndex = 14
        Me.numRandomizeOffWidth.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numRandomizePosition
        '
        Me.numRandomizePosition.Location = New System.Drawing.Point(129, 208)
        Me.numRandomizePosition.Maximum = New Decimal(New Integer() {5, 0, 0, 0})
        Me.numRandomizePosition.Name = "numRandomizePosition"
        Me.numRandomizePosition.Size = New System.Drawing.Size(80, 22)
        Me.numRandomizePosition.TabIndex = 14
        Me.numRandomizePosition.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'NumRandomizePhase
        '
        Me.NumRandomizePhase.Location = New System.Drawing.Point(129, 162)
        Me.NumRandomizePhase.Maximum = New Decimal(New Integer() {180, 0, 0, 0})
        Me.NumRandomizePhase.Name = "NumRandomizePhase"
        Me.NumRandomizePhase.Size = New System.Drawing.Size(80, 22)
        Me.NumRandomizePhase.TabIndex = 14
        Me.NumRandomizePhase.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numRandomizeFrequency
        '
        Me.numRandomizeFrequency.Location = New System.Drawing.Point(18, 163)
        Me.numRandomizeFrequency.Maximum = New Decimal(New Integer() {50, 0, 0, 0})
        Me.numRandomizeFrequency.Name = "numRandomizeFrequency"
        Me.numRandomizeFrequency.Size = New System.Drawing.Size(80, 22)
        Me.numRandomizeFrequency.TabIndex = 14
        Me.numRandomizeFrequency.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'chkRandomizePhase
        '
        Me.chkRandomizePhase.AutoSize = True
        Me.chkRandomizePhase.Location = New System.Drawing.Point(129, 144)
        Me.chkRandomizePhase.Name = "chkRandomizePhase"
        Me.chkRandomizePhase.Size = New System.Drawing.Size(66, 20)
        Me.chkRandomizePhase.TabIndex = 15
        Me.chkRandomizePhase.Text = "Phase"
        Me.chkRandomizePhase.UseVisualStyleBackColor = True
        '
        'chkRandomizePosition
        '
        Me.chkRandomizePosition.AutoSize = True
        Me.chkRandomizePosition.Location = New System.Drawing.Point(129, 190)
        Me.chkRandomizePosition.Name = "chkRandomizePosition"
        Me.chkRandomizePosition.Size = New System.Drawing.Size(75, 20)
        Me.chkRandomizePosition.TabIndex = 15
        Me.chkRandomizePosition.Text = "Position"
        Me.chkRandomizePosition.UseVisualStyleBackColor = True
        '
        'chkOffWidth
        '
        Me.chkOffWidth.AutoSize = True
        Me.chkOffWidth.Location = New System.Drawing.Point(17, 190)
        Me.chkOffWidth.Name = "chkOffWidth"
        Me.chkOffWidth.Size = New System.Drawing.Size(75, 20)
        Me.chkOffWidth.TabIndex = 15
        Me.chkOffWidth.Text = "Off state"
        Me.chkOffWidth.UseVisualStyleBackColor = True
        '
        'chkRandomizeFrequency
        '
        Me.chkRandomizeFrequency.AutoSize = True
        Me.chkRandomizeFrequency.Location = New System.Drawing.Point(19, 145)
        Me.chkRandomizeFrequency.Name = "chkRandomizeFrequency"
        Me.chkRandomizeFrequency.Size = New System.Drawing.Size(91, 20)
        Me.chkRandomizeFrequency.TabIndex = 15
        Me.chkRandomizeFrequency.Text = "Frequency"
        Me.chkRandomizeFrequency.UseVisualStyleBackColor = True
        '
        'numSpacingSizeY
        '
        Me.numSpacingSizeY.Location = New System.Drawing.Point(161, 64)
        Me.numSpacingSizeY.Name = "numSpacingSizeY"
        Me.numSpacingSizeY.Size = New System.Drawing.Size(55, 22)
        Me.numSpacingSizeY.TabIndex = 14
        Me.numSpacingSizeY.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numSpacingSizeX
        '
        Me.numSpacingSizeX.Location = New System.Drawing.Point(94, 64)
        Me.numSpacingSizeX.Name = "numSpacingSizeX"
        Me.numSpacingSizeX.Size = New System.Drawing.Size(55, 22)
        Me.numSpacingSizeX.TabIndex = 14
        Me.numSpacingSizeX.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numArraySizeY
        '
        Me.numArraySizeY.Increment = New Decimal(New Integer() {2, 0, 0, 0})
        Me.numArraySizeY.Location = New System.Drawing.Point(161, 36)
        Me.numArraySizeY.Maximum = New Decimal(New Integer() {101, 0, 0, 0})
        Me.numArraySizeY.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numArraySizeY.Name = "numArraySizeY"
        Me.numArraySizeY.Size = New System.Drawing.Size(55, 22)
        Me.numArraySizeY.TabIndex = 14
        Me.numArraySizeY.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numArraySizeY.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'numArraySizeX
        '
        Me.numArraySizeX.Increment = New Decimal(New Integer() {2, 0, 0, 0})
        Me.numArraySizeX.Location = New System.Drawing.Point(93, 36)
        Me.numArraySizeX.Maximum = New Decimal(New Integer() {101, 0, 0, 0})
        Me.numArraySizeX.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numArraySizeX.Name = "numArraySizeX"
        Me.numArraySizeX.Size = New System.Drawing.Size(55, 22)
        Me.numArraySizeX.TabIndex = 14
        Me.numArraySizeX.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numArraySizeX.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'Label50
        '
        Me.Label50.AutoSize = True
        Me.Label50.Location = New System.Drawing.Point(15, 124)
        Me.Label50.Name = "Label50"
        Me.Label50.Size = New System.Drawing.Size(77, 16)
        Me.Label50.TabIndex = 0
        Me.Label50.Text = "Randomize"
        '
        'Label51
        '
        Me.Label51.AutoSize = True
        Me.Label51.Location = New System.Drawing.Point(15, 96)
        Me.Label51.Name = "Label51"
        Me.Label51.Size = New System.Drawing.Size(70, 16)
        Me.Label51.TabIndex = 0
        Me.Label51.Text = "Properties"
        '
        'Label52
        '
        Me.Label52.AutoSize = True
        Me.Label52.Location = New System.Drawing.Point(15, 66)
        Me.Label52.Name = "Label52"
        Me.Label52.Size = New System.Drawing.Size(61, 16)
        Me.Label52.TabIndex = 0
        Me.Label52.Text = "Spacing "
        '
        'Label53
        '
        Me.Label53.AutoSize = True
        Me.Label53.Location = New System.Drawing.Point(15, 36)
        Me.Label53.Name = "Label53"
        Me.Label53.Size = New System.Drawing.Size(67, 16)
        Me.Label53.TabIndex = 0
        Me.Label53.Text = "Array size"
        '
        'cmbCompositeProperties
        '
        Me.cmbCompositeProperties.Enabled = False
        Me.cmbCompositeProperties.FormattingEnabled = True
        Me.cmbCompositeProperties.Items.AddRange(New Object() {"Inherit from single", "Inherit + randomize"})
        Me.cmbCompositeProperties.Location = New System.Drawing.Point(88, 93)
        Me.cmbCompositeProperties.Name = "cmbCompositeProperties"
        Me.cmbCompositeProperties.Size = New System.Drawing.Size(134, 24)
        Me.cmbCompositeProperties.TabIndex = 13
        '
        'grpSingleSource
        '
        Me.grpSingleSource.Controls.Add(Me.numOffStateCycles)
        Me.grpSingleSource.Controls.Add(Me.numPhase)
        Me.grpSingleSource.Controls.Add(Me.numAmplitude)
        Me.grpSingleSource.Controls.Add(Me.numFrequency)
        Me.grpSingleSource.Controls.Add(Me.numPulseWidthCycles)
        Me.grpSingleSource.Controls.Add(Me.Label44)
        Me.grpSingleSource.Controls.Add(Me.Label46)
        Me.grpSingleSource.Controls.Add(Me.Label45)
        Me.grpSingleSource.Controls.Add(Me.Label43)
        Me.grpSingleSource.Controls.Add(Me.Label42)
        Me.grpSingleSource.Controls.Add(Me.Label54)
        Me.grpSingleSource.Controls.Add(Me.Label41)
        Me.grpSingleSource.Controls.Add(Me.Label40)
        Me.grpSingleSource.Controls.Add(Me.cmbPulseShape)
        Me.grpSingleSource.Location = New System.Drawing.Point(12, 126)
        Me.grpSingleSource.Name = "grpSingleSource"
        Me.grpSingleSource.Size = New System.Drawing.Size(230, 213)
        Me.grpSingleSource.TabIndex = 16
        Me.grpSingleSource.TabStop = False
        Me.grpSingleSource.Text = "Single source properties"
        '
        'numOffStateCycles
        '
        Me.numOffStateCycles.Location = New System.Drawing.Point(106, 179)
        Me.numOffStateCycles.Maximum = New Decimal(New Integer() {100000, 0, 0, 0})
        Me.numOffStateCycles.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numOffStateCycles.Name = "numOffStateCycles"
        Me.numOffStateCycles.Size = New System.Drawing.Size(64, 22)
        Me.numOffStateCycles.TabIndex = 14
        Me.numOffStateCycles.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numOffStateCycles.Value = New Decimal(New Integer() {10, 0, 0, 0})
        '
        'numPhase
        '
        Me.numPhase.Location = New System.Drawing.Point(106, 87)
        Me.numPhase.Maximum = New Decimal(New Integer() {180, 0, 0, 0})
        Me.numPhase.Minimum = New Decimal(New Integer() {180, 0, 0, -2147483648})
        Me.numPhase.Name = "numPhase"
        Me.numPhase.Size = New System.Drawing.Size(64, 22)
        Me.numPhase.TabIndex = 14
        Me.numPhase.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numAmplitude
        '
        Me.numAmplitude.Location = New System.Drawing.Point(106, 59)
        Me.numAmplitude.Name = "numAmplitude"
        Me.numAmplitude.Size = New System.Drawing.Size(64, 22)
        Me.numAmplitude.TabIndex = 14
        Me.numAmplitude.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numAmplitude.Value = New Decimal(New Integer() {100, 0, 0, 0})
        '
        'numFrequency
        '
        Me.numFrequency.Location = New System.Drawing.Point(106, 31)
        Me.numFrequency.Maximum = New Decimal(New Integer() {255, 0, 0, 0})
        Me.numFrequency.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numFrequency.Name = "numFrequency"
        Me.numFrequency.Size = New System.Drawing.Size(64, 22)
        Me.numFrequency.TabIndex = 14
        Me.numFrequency.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numFrequency.Value = New Decimal(New Integer() {100, 0, 0, 0})
        '
        'numPulseWidthCycles
        '
        Me.numPulseWidthCycles.Location = New System.Drawing.Point(106, 151)
        Me.numPulseWidthCycles.Name = "numPulseWidthCycles"
        Me.numPulseWidthCycles.Size = New System.Drawing.Size(64, 22)
        Me.numPulseWidthCycles.TabIndex = 14
        Me.numPulseWidthCycles.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numPulseWidthCycles.Value = New Decimal(New Integer() {10, 0, 0, 0})
        '
        'Label44
        '
        Me.Label44.AutoSize = True
        Me.Label44.Location = New System.Drawing.Point(15, 182)
        Me.Label44.Name = "Label44"
        Me.Label44.Size = New System.Drawing.Size(56, 16)
        Me.Label44.TabIndex = 0
        Me.Label44.Text = "Off state"
        '
        'Label46
        '
        Me.Label46.AutoSize = True
        Me.Label46.Location = New System.Drawing.Point(176, 181)
        Me.Label46.Name = "Label46"
        Me.Label46.Size = New System.Drawing.Size(47, 16)
        Me.Label46.TabIndex = 0
        Me.Label46.Text = "cycles"
        '
        'Label45
        '
        Me.Label45.AutoSize = True
        Me.Label45.Location = New System.Drawing.Point(176, 153)
        Me.Label45.Name = "Label45"
        Me.Label45.Size = New System.Drawing.Size(47, 16)
        Me.Label45.TabIndex = 0
        Me.Label45.Text = "cycles"
        '
        'Label43
        '
        Me.Label43.AutoSize = True
        Me.Label43.Location = New System.Drawing.Point(15, 153)
        Me.Label43.Name = "Label43"
        Me.Label43.Size = New System.Drawing.Size(75, 16)
        Me.Label43.TabIndex = 0
        Me.Label43.Text = "Pulse width"
        '
        'Label42
        '
        Me.Label42.AutoSize = True
        Me.Label42.Location = New System.Drawing.Point(15, 121)
        Me.Label42.Name = "Label42"
        Me.Label42.Size = New System.Drawing.Size(83, 16)
        Me.Label42.TabIndex = 0
        Me.Label42.Text = "Pulse shape"
        '
        'Label54
        '
        Me.Label54.AutoSize = True
        Me.Label54.Location = New System.Drawing.Point(14, 89)
        Me.Label54.Name = "Label54"
        Me.Label54.Size = New System.Drawing.Size(47, 16)
        Me.Label54.TabIndex = 0
        Me.Label54.Text = "Phase"
        '
        'Label41
        '
        Me.Label41.AutoSize = True
        Me.Label41.Location = New System.Drawing.Point(14, 61)
        Me.Label41.Name = "Label41"
        Me.Label41.Size = New System.Drawing.Size(68, 16)
        Me.Label41.TabIndex = 0
        Me.Label41.Text = "Amplitude"
        '
        'Label40
        '
        Me.Label40.AutoSize = True
        Me.Label40.Location = New System.Drawing.Point(14, 33)
        Me.Label40.Name = "Label40"
        Me.Label40.Size = New System.Drawing.Size(72, 16)
        Me.Label40.TabIndex = 0
        Me.Label40.Text = "Frequency"
        '
        'cmbPulseShape
        '
        Me.cmbPulseShape.Enabled = False
        Me.cmbPulseShape.FormattingEnabled = True
        Me.cmbPulseShape.Items.AddRange(New Object() {"Gaussian", "Smoothed square"})
        Me.cmbPulseShape.Location = New System.Drawing.Point(106, 118)
        Me.cmbPulseShape.Name = "cmbPulseShape"
        Me.cmbPulseShape.Size = New System.Drawing.Size(110, 24)
        Me.cmbPulseShape.TabIndex = 13
        '
        'radModePulsed
        '
        Me.radModePulsed.AutoSize = True
        Me.radModePulsed.Location = New System.Drawing.Point(156, 97)
        Me.radModePulsed.Name = "radModePulsed"
        Me.radModePulsed.Size = New System.Drawing.Size(68, 20)
        Me.radModePulsed.TabIndex = 15
        Me.radModePulsed.TabStop = True
        Me.radModePulsed.Text = "Pulsed"
        Me.radModePulsed.UseVisualStyleBackColor = True
        '
        'radModeContinuous
        '
        Me.radModeContinuous.AutoSize = True
        Me.radModeContinuous.Location = New System.Drawing.Point(65, 97)
        Me.radModeContinuous.Name = "radModeContinuous"
        Me.radModeContinuous.Size = New System.Drawing.Size(92, 20)
        Me.radModeContinuous.TabIndex = 15
        Me.radModeContinuous.TabStop = True
        Me.radModeContinuous.Text = "Continuous"
        Me.radModeContinuous.UseVisualStyleBackColor = True
        '
        'txtName
        '
        Me.txtName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtName.Location = New System.Drawing.Point(92, 30)
        Me.txtName.Name = "txtName"
        Me.txtName.Size = New System.Drawing.Size(142, 22)
        Me.txtName.TabIndex = 14
        '
        'cmbMainType
        '
        Me.cmbMainType.FormattingEnabled = True
        Me.cmbMainType.Items.AddRange(New Object() {"Single point", "Composite"})
        Me.cmbMainType.Location = New System.Drawing.Point(92, 61)
        Me.cmbMainType.Name = "cmbMainType"
        Me.cmbMainType.Size = New System.Drawing.Size(142, 24)
        Me.cmbMainType.TabIndex = 13
        '
        'Label39
        '
        Me.Label39.AutoSize = True
        Me.Label39.Location = New System.Drawing.Point(20, 99)
        Me.Label39.Name = "Label39"
        Me.Label39.Size = New System.Drawing.Size(43, 16)
        Me.Label39.TabIndex = 12
        Me.Label39.Text = "Mode"
        '
        'lblName
        '
        Me.lblName.AutoSize = True
        Me.lblName.Location = New System.Drawing.Point(20, 33)
        Me.lblName.Name = "lblName"
        Me.lblName.Size = New System.Drawing.Size(45, 16)
        Me.lblName.TabIndex = 12
        Me.lblName.Text = "Name"
        '
        'Label38
        '
        Me.Label38.AutoSize = True
        Me.Label38.Location = New System.Drawing.Point(20, 64)
        Me.Label38.Name = "Label38"
        Me.Label38.Size = New System.Drawing.Size(66, 16)
        Me.Label38.TabIndex = 12
        Me.Label38.Text = "Main type"
        '
        'tabProbeList
        '
        Me.tabProbeList.Controls.Add(Me.GroupBox2)
        Me.tabProbeList.Controls.Add(Me.grpProbe2)
        Me.tabProbeList.Controls.Add(Me.grpProbe1)
        Me.tabProbeList.Controls.Add(Me.pnlProbeImage)
        Me.tabProbeList.Location = New System.Drawing.Point(4, 34)
        Me.tabProbeList.Name = "tabProbeList"
        Me.tabProbeList.Size = New System.Drawing.Size(1742, 693)
        Me.tabProbeList.TabIndex = 2
        Me.tabProbeList.Text = "      Probes      "
        Me.tabProbeList.UseVisualStyleBackColor = True
        '
        'GroupBox2
        '
        Me.GroupBox2.Controls.Add(Me.Label49)
        Me.GroupBox2.Controls.Add(Me.Label48)
        Me.GroupBox2.Controls.Add(Me.Label37)
        Me.GroupBox2.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.GroupBox2.Location = New System.Drawing.Point(1201, 584)
        Me.GroupBox2.Name = "GroupBox2"
        Me.GroupBox2.Size = New System.Drawing.Size(504, 83)
        Me.GroupBox2.TabIndex = 2
        Me.GroupBox2.TabStop = False
        Me.GroupBox2.Text = "How to use probes"
        '
        'Label49
        '
        Me.Label49.AutoSize = True
        Me.Label49.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label49.Location = New System.Drawing.Point(15, 60)
        Me.Label49.Name = "Label49"
        Me.Label49.Size = New System.Drawing.Size(430, 16)
        Me.Label49.TabIndex = 0
        Me.Label49.Text = "- Probe type ""Line"":  Use 2 clicks to set line, then third click to place plot ."
        '
        'Label48
        '
        Me.Label48.AutoSize = True
        Me.Label48.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label48.Location = New System.Drawing.Point(14, 42)
        Me.Label48.Name = "Label48"
        Me.Label48.Size = New System.Drawing.Size(461, 16)
        Me.Label48.TabIndex = 0
        Me.Label48.Text = "- Probe type ""Point"":  First click sets probe location, second sets data window"
        '
        'Label37
        '
        Me.Label37.AutoSize = True
        Me.Label37.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label37.Location = New System.Drawing.Point(15, 22)
        Me.Label37.Name = "Label37"
        Me.Label37.Size = New System.Drawing.Size(442, 16)
        Me.Label37.TabIndex = 0
        Me.Label37.Text = "Set all properties first and then  use the ""Pick"" button to choose a location. "
        '
        'grpProbe2
        '
        Me.grpProbe2.BackColor = System.Drawing.Color.Gainsboro
        Me.grpProbe2.Controls.Add(Me.Label22)
        Me.grpProbe2.Controls.Add(Me.numRunningWindow2)
        Me.grpProbe2.Controls.Add(Me.chkProbeOn2)
        Me.grpProbe2.Controls.Add(Me.txtStartAfterStep2)
        Me.grpProbe2.Controls.Add(Me.chkStartAfterStep2)
        Me.grpProbe2.Controls.Add(Me.Label23)
        Me.grpProbe2.Controls.Add(Me.Label24)
        Me.grpProbe2.Controls.Add(Me.txtMaxScaleY2)
        Me.grpProbe2.Controls.Add(Me.txtMinScaleY2)
        Me.grpProbe2.Controls.Add(Me.radFullScale2)
        Me.grpProbe2.Controls.Add(Me.radRunningScale2)
        Me.grpProbe2.Controls.Add(Me.chkAutoScaleY2)
        Me.grpProbe2.Controls.Add(Me.chkAutoScaleX2)
        Me.grpProbe2.Controls.Add(Me.numBPosY2)
        Me.grpProbe2.Controls.Add(Me.numAPosY2)
        Me.grpProbe2.Controls.Add(Me.numBPosX2)
        Me.grpProbe2.Controls.Add(Me.numAPosX2)
        Me.grpProbe2.Controls.Add(Me.butPicPositions2)
        Me.grpProbe2.Controls.Add(Me.Label25)
        Me.grpProbe2.Controls.Add(Me.cmbPlaceOn2)
        Me.grpProbe2.Controls.Add(Me.cmbMeasurement2)
        Me.grpProbe2.Controls.Add(Me.Label26)
        Me.grpProbe2.Controls.Add(Me.Label27)
        Me.grpProbe2.Controls.Add(Me.cmbBoxSize2)
        Me.grpProbe2.Controls.Add(Me.cmbProbeType2)
        Me.grpProbe2.Controls.Add(Me.Label28)
        Me.grpProbe2.Controls.Add(Me.Label29)
        Me.grpProbe2.Controls.Add(Me.Label30)
        Me.grpProbe2.Controls.Add(Me.Label31)
        Me.grpProbe2.Controls.Add(Me.Label32)
        Me.grpProbe2.Controls.Add(Me.Label33)
        Me.grpProbe2.Controls.Add(Me.Label34)
        Me.grpProbe2.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpProbe2.Location = New System.Drawing.Point(1202, 303)
        Me.grpProbe2.Name = "grpProbe2"
        Me.grpProbe2.Size = New System.Drawing.Size(506, 276)
        Me.grpProbe2.TabIndex = 1
        Me.grpProbe2.TabStop = False
        Me.grpProbe2.Text = "Probe 2                   "
        '
        'Label22
        '
        Me.Label22.AutoSize = True
        Me.Label22.Location = New System.Drawing.Point(259, 59)
        Me.Label22.Name = "Label22"
        Me.Label22.Size = New System.Drawing.Size(104, 16)
        Me.Label22.TabIndex = 14
        Me.Label22.Text = "Running window"
        '
        'numRunningWindow2
        '
        Me.numRunningWindow2.Location = New System.Drawing.Point(371, 58)
        Me.numRunningWindow2.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numRunningWindow2.Minimum = New Decimal(New Integer() {10, 0, 0, 0})
        Me.numRunningWindow2.Name = "numRunningWindow2"
        Me.numRunningWindow2.Size = New System.Drawing.Size(83, 22)
        Me.numRunningWindow2.TabIndex = 13
        Me.numRunningWindow2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numRunningWindow2.Value = New Decimal(New Integer() {500, 0, 0, 0})
        '
        'chkProbeOn2
        '
        Me.chkProbeOn2.AutoSize = True
        Me.chkProbeOn2.Location = New System.Drawing.Point(66, -1)
        Me.chkProbeOn2.Name = "chkProbeOn2"
        Me.chkProbeOn2.Size = New System.Drawing.Size(44, 20)
        Me.chkProbeOn2.TabIndex = 12
        Me.chkProbeOn2.Text = "On"
        Me.chkProbeOn2.UseVisualStyleBackColor = True
        '
        'txtStartAfterStep2
        '
        Me.txtStartAfterStep2.Location = New System.Drawing.Point(179, 163)
        Me.txtStartAfterStep2.Name = "txtStartAfterStep2"
        Me.txtStartAfterStep2.Size = New System.Drawing.Size(57, 22)
        Me.txtStartAfterStep2.TabIndex = 11
        Me.txtStartAfterStep2.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        '
        'chkStartAfterStep2
        '
        Me.chkStartAfterStep2.AutoSize = True
        Me.chkStartAfterStep2.Location = New System.Drawing.Point(17, 165)
        Me.chkStartAfterStep2.Name = "chkStartAfterStep2"
        Me.chkStartAfterStep2.Size = New System.Drawing.Size(167, 20)
        Me.chkStartAfterStep2.TabIndex = 10
        Me.chkStartAfterStep2.Text = "Start recording at step #"
        Me.chkStartAfterStep2.UseVisualStyleBackColor = True
        '
        'Label23
        '
        Me.Label23.AutoSize = True
        Me.Label23.Location = New System.Drawing.Point(438, 111)
        Me.Label23.Name = "Label23"
        Me.Label23.Size = New System.Drawing.Size(33, 16)
        Me.Label23.TabIndex = 2
        Me.Label23.Text = "Max"
        '
        'Label24
        '
        Me.Label24.AutoSize = True
        Me.Label24.Location = New System.Drawing.Point(378, 111)
        Me.Label24.Name = "Label24"
        Me.Label24.Size = New System.Drawing.Size(29, 16)
        Me.Label24.TabIndex = 2
        Me.Label24.Text = "Min"
        '
        'txtMaxScaleY2
        '
        Me.txtMaxScaleY2.Location = New System.Drawing.Point(431, 127)
        Me.txtMaxScaleY2.Name = "txtMaxScaleY2"
        Me.txtMaxScaleY2.Size = New System.Drawing.Size(52, 22)
        Me.txtMaxScaleY2.TabIndex = 8
        Me.txtMaxScaleY2.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        '
        'txtMinScaleY2
        '
        Me.txtMinScaleY2.Location = New System.Drawing.Point(371, 127)
        Me.txtMinScaleY2.Name = "txtMinScaleY2"
        Me.txtMinScaleY2.Size = New System.Drawing.Size(52, 22)
        Me.txtMinScaleY2.TabIndex = 8
        Me.txtMinScaleY2.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        '
        'radFullScale2
        '
        Me.radFullScale2.AutoSize = True
        Me.radFullScale2.Location = New System.Drawing.Point(379, 24)
        Me.radFullScale2.Name = "radFullScale2"
        Me.radFullScale2.Size = New System.Drawing.Size(83, 20)
        Me.radFullScale2.TabIndex = 7
        Me.radFullScale2.TabStop = True
        Me.radFullScale2.Text = "Full scale"
        Me.radFullScale2.UseVisualStyleBackColor = True
        '
        'radRunningScale2
        '
        Me.radRunningScale2.AutoSize = True
        Me.radRunningScale2.Location = New System.Drawing.Point(262, 24)
        Me.radRunningScale2.Name = "radRunningScale2"
        Me.radRunningScale2.Size = New System.Drawing.Size(111, 20)
        Me.radRunningScale2.TabIndex = 7
        Me.radRunningScale2.TabStop = True
        Me.radRunningScale2.Text = "Running scale"
        Me.radRunningScale2.UseVisualStyleBackColor = True
        '
        'chkAutoScaleY2
        '
        Me.chkAutoScaleY2.AutoSize = True
        Me.chkAutoScaleY2.Location = New System.Drawing.Point(262, 127)
        Me.chkAutoScaleY2.Name = "chkAutoScaleY2"
        Me.chkAutoScaleY2.Size = New System.Drawing.Size(110, 20)
        Me.chkAutoScaleY2.TabIndex = 6
        Me.chkAutoScaleY2.Text = "Auto   Scale Y"
        Me.chkAutoScaleY2.UseVisualStyleBackColor = True
        '
        'chkAutoScaleX2
        '
        Me.chkAutoScaleX2.AutoSize = True
        Me.chkAutoScaleX2.Location = New System.Drawing.Point(262, 91)
        Me.chkAutoScaleX2.Name = "chkAutoScaleX2"
        Me.chkAutoScaleX2.Size = New System.Drawing.Size(101, 20)
        Me.chkAutoScaleX2.TabIndex = 6
        Me.chkAutoScaleX2.Text = "Auto scale X"
        Me.chkAutoScaleX2.UseVisualStyleBackColor = True
        '
        'numBPosY2
        '
        Me.numBPosY2.Location = New System.Drawing.Point(326, 234)
        Me.numBPosY2.Name = "numBPosY2"
        Me.numBPosY2.Size = New System.Drawing.Size(65, 22)
        Me.numBPosY2.TabIndex = 5
        Me.numBPosY2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numAPosY2
        '
        Me.numAPosY2.Location = New System.Drawing.Point(126, 234)
        Me.numAPosY2.Name = "numAPosY2"
        Me.numAPosY2.Size = New System.Drawing.Size(65, 22)
        Me.numAPosY2.TabIndex = 5
        Me.numAPosY2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numBPosX2
        '
        Me.numBPosX2.Location = New System.Drawing.Point(238, 234)
        Me.numBPosX2.Name = "numBPosX2"
        Me.numBPosX2.Size = New System.Drawing.Size(65, 22)
        Me.numBPosX2.TabIndex = 5
        Me.numBPosX2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numAPosX2
        '
        Me.numAPosX2.Location = New System.Drawing.Point(38, 234)
        Me.numAPosX2.Name = "numAPosX2"
        Me.numAPosX2.Size = New System.Drawing.Size(65, 22)
        Me.numAPosX2.TabIndex = 5
        Me.numAPosX2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'butPicPositions2
        '
        Me.butPicPositions2.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.butPicPositions2.Location = New System.Drawing.Point(402, 226)
        Me.butPicPositions2.Name = "butPicPositions2"
        Me.butPicPositions2.Size = New System.Drawing.Size(90, 34)
        Me.butPicPositions2.TabIndex = 4
        Me.butPicPositions2.Text = "Pick "
        Me.butPicPositions2.UseVisualStyleBackColor = True
        '
        'Label25
        '
        Me.Label25.AutoSize = True
        Me.Label25.Location = New System.Drawing.Point(311, 236)
        Me.Label25.Name = "Label25"
        Me.Label25.Size = New System.Drawing.Size(17, 16)
        Me.Label25.TabIndex = 2
        Me.Label25.Text = "Y"
        '
        'cmbPlaceOn2
        '
        Me.cmbPlaceOn2.FormattingEnabled = True
        Me.cmbPlaceOn2.Items.AddRange(New Object() {"Field", "Intensity"})
        Me.cmbPlaceOn2.Location = New System.Drawing.Point(106, 24)
        Me.cmbPlaceOn2.Name = "cmbPlaceOn2"
        Me.cmbPlaceOn2.Size = New System.Drawing.Size(130, 24)
        Me.cmbPlaceOn2.TabIndex = 3
        '
        'cmbMeasurement2
        '
        Me.cmbMeasurement2.FormattingEnabled = True
        Me.cmbMeasurement2.Items.AddRange(New Object() {"Amplitude", "Intensity", "Integrate Intensity"})
        Me.cmbMeasurement2.Location = New System.Drawing.Point(106, 56)
        Me.cmbMeasurement2.Name = "cmbMeasurement2"
        Me.cmbMeasurement2.Size = New System.Drawing.Size(130, 24)
        Me.cmbMeasurement2.TabIndex = 3
        '
        'Label26
        '
        Me.Label26.AutoSize = True
        Me.Label26.Location = New System.Drawing.Point(111, 236)
        Me.Label26.Name = "Label26"
        Me.Label26.Size = New System.Drawing.Size(17, 16)
        Me.Label26.TabIndex = 2
        Me.Label26.Text = "Y"
        '
        'Label27
        '
        Me.Label27.AutoSize = True
        Me.Label27.Location = New System.Drawing.Point(223, 236)
        Me.Label27.Name = "Label27"
        Me.Label27.Size = New System.Drawing.Size(16, 16)
        Me.Label27.TabIndex = 2
        Me.Label27.Text = "X"
        '
        'cmbBoxSize2
        '
        Me.cmbBoxSize2.FormattingEnabled = True
        Me.cmbBoxSize2.Items.AddRange(New Object() {"Tiny (200 x100)", "Small (300 x 150)", "Medium (400x200)", "Large (500x250)", "Extra Large (700x350)"})
        Me.cmbBoxSize2.Location = New System.Drawing.Point(106, 124)
        Me.cmbBoxSize2.Name = "cmbBoxSize2"
        Me.cmbBoxSize2.Size = New System.Drawing.Size(130, 24)
        Me.cmbBoxSize2.TabIndex = 3
        '
        'cmbProbeType2
        '
        Me.cmbProbeType2.FormattingEnabled = True
        Me.cmbProbeType2.Items.AddRange(New Object() {"Point (A)", "Line (A-B)"})
        Me.cmbProbeType2.Location = New System.Drawing.Point(106, 89)
        Me.cmbProbeType2.Name = "cmbProbeType2"
        Me.cmbProbeType2.Size = New System.Drawing.Size(130, 24)
        Me.cmbProbeType2.TabIndex = 3
        '
        'Label28
        '
        Me.Label28.AutoSize = True
        Me.Label28.Location = New System.Drawing.Point(277, 209)
        Me.Label28.Name = "Label28"
        Me.Label28.Size = New System.Drawing.Size(68, 16)
        Me.Label28.TabIndex = 2
        Me.Label28.Text = "Position B"
        '
        'Label29
        '
        Me.Label29.AutoSize = True
        Me.Label29.Location = New System.Drawing.Point(23, 236)
        Me.Label29.Name = "Label29"
        Me.Label29.Size = New System.Drawing.Size(16, 16)
        Me.Label29.TabIndex = 2
        Me.Label29.Text = "X"
        '
        'Label30
        '
        Me.Label30.AutoSize = True
        Me.Label30.Location = New System.Drawing.Point(76, 209)
        Me.Label30.Name = "Label30"
        Me.Label30.Size = New System.Drawing.Size(68, 16)
        Me.Label30.TabIndex = 2
        Me.Label30.Text = "Position A"
        '
        'Label31
        '
        Me.Label31.AutoSize = True
        Me.Label31.Location = New System.Drawing.Point(14, 127)
        Me.Label31.Name = "Label31"
        Me.Label31.Size = New System.Drawing.Size(60, 16)
        Me.Label31.TabIndex = 2
        Me.Label31.Text = "Box Size"
        '
        'Label32
        '
        Me.Label32.AutoSize = True
        Me.Label32.Location = New System.Drawing.Point(14, 92)
        Me.Label32.Name = "Label32"
        Me.Label32.Size = New System.Drawing.Size(74, 16)
        Me.Label32.TabIndex = 2
        Me.Label32.Text = "Probe type"
        '
        'Label33
        '
        Me.Label33.AutoSize = True
        Me.Label33.Location = New System.Drawing.Point(12, 59)
        Me.Label33.Name = "Label33"
        Me.Label33.Size = New System.Drawing.Size(90, 16)
        Me.Label33.TabIndex = 2
        Me.Label33.Text = "Measurement"
        '
        'Label34
        '
        Me.Label34.AutoSize = True
        Me.Label34.Location = New System.Drawing.Point(14, 29)
        Me.Label34.Name = "Label34"
        Me.Label34.Size = New System.Drawing.Size(64, 16)
        Me.Label34.TabIndex = 1
        Me.Label34.Text = "Place on:"
        '
        'grpProbe1
        '
        Me.grpProbe1.BackColor = System.Drawing.Color.Gainsboro
        Me.grpProbe1.Controls.Add(Me.Label21)
        Me.grpProbe1.Controls.Add(Me.numRunningWindow)
        Me.grpProbe1.Controls.Add(Me.chkProbeOn1)
        Me.grpProbe1.Controls.Add(Me.txtStartAfterStep)
        Me.grpProbe1.Controls.Add(Me.chkStartAfterStep)
        Me.grpProbe1.Controls.Add(Me.Label19)
        Me.grpProbe1.Controls.Add(Me.Label18)
        Me.grpProbe1.Controls.Add(Me.txtMaxScaleY)
        Me.grpProbe1.Controls.Add(Me.txtMinScaleY)
        Me.grpProbe1.Controls.Add(Me.radFullScale)
        Me.grpProbe1.Controls.Add(Me.radRunningScale)
        Me.grpProbe1.Controls.Add(Me.chkAutoScaleY)
        Me.grpProbe1.Controls.Add(Me.chkAutoScaleX)
        Me.grpProbe1.Controls.Add(Me.numBPosY)
        Me.grpProbe1.Controls.Add(Me.numAPosY)
        Me.grpProbe1.Controls.Add(Me.numBPosX)
        Me.grpProbe1.Controls.Add(Me.numAPosX)
        Me.grpProbe1.Controls.Add(Me.butPicPositions)
        Me.grpProbe1.Controls.Add(Me.Label16)
        Me.grpProbe1.Controls.Add(Me.cmbPlaceOn)
        Me.grpProbe1.Controls.Add(Me.cmbMeasurement)
        Me.grpProbe1.Controls.Add(Me.Label13)
        Me.grpProbe1.Controls.Add(Me.Label15)
        Me.grpProbe1.Controls.Add(Me.cmbBoxSize)
        Me.grpProbe1.Controls.Add(Me.cmbProbeType)
        Me.grpProbe1.Controls.Add(Me.Label14)
        Me.grpProbe1.Controls.Add(Me.Label12)
        Me.grpProbe1.Controls.Add(Me.Label11)
        Me.grpProbe1.Controls.Add(Me.Label17)
        Me.grpProbe1.Controls.Add(Me.Label10)
        Me.grpProbe1.Controls.Add(Me.Label9)
        Me.grpProbe1.Controls.Add(Me.Label8)
        Me.grpProbe1.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpProbe1.Location = New System.Drawing.Point(1202, 14)
        Me.grpProbe1.Name = "grpProbe1"
        Me.grpProbe1.Size = New System.Drawing.Size(506, 276)
        Me.grpProbe1.TabIndex = 1
        Me.grpProbe1.TabStop = False
        Me.grpProbe1.Text = "Probe 1                   "
        '
        'Label21
        '
        Me.Label21.AutoSize = True
        Me.Label21.Location = New System.Drawing.Point(259, 59)
        Me.Label21.Name = "Label21"
        Me.Label21.Size = New System.Drawing.Size(104, 16)
        Me.Label21.TabIndex = 14
        Me.Label21.Text = "Running window"
        '
        'numRunningWindow
        '
        Me.numRunningWindow.Location = New System.Drawing.Point(371, 58)
        Me.numRunningWindow.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numRunningWindow.Minimum = New Decimal(New Integer() {10, 0, 0, 0})
        Me.numRunningWindow.Name = "numRunningWindow"
        Me.numRunningWindow.Size = New System.Drawing.Size(83, 22)
        Me.numRunningWindow.TabIndex = 13
        Me.numRunningWindow.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numRunningWindow.Value = New Decimal(New Integer() {500, 0, 0, 0})
        '
        'chkProbeOn1
        '
        Me.chkProbeOn1.AutoSize = True
        Me.chkProbeOn1.Location = New System.Drawing.Point(66, -1)
        Me.chkProbeOn1.Name = "chkProbeOn1"
        Me.chkProbeOn1.Size = New System.Drawing.Size(44, 20)
        Me.chkProbeOn1.TabIndex = 12
        Me.chkProbeOn1.Text = "On"
        Me.chkProbeOn1.UseVisualStyleBackColor = True
        '
        'txtStartAfterStep
        '
        Me.txtStartAfterStep.Location = New System.Drawing.Point(179, 163)
        Me.txtStartAfterStep.Name = "txtStartAfterStep"
        Me.txtStartAfterStep.Size = New System.Drawing.Size(57, 22)
        Me.txtStartAfterStep.TabIndex = 11
        Me.txtStartAfterStep.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        '
        'chkStartAfterStep
        '
        Me.chkStartAfterStep.AutoSize = True
        Me.chkStartAfterStep.Location = New System.Drawing.Point(17, 165)
        Me.chkStartAfterStep.Name = "chkStartAfterStep"
        Me.chkStartAfterStep.Size = New System.Drawing.Size(167, 20)
        Me.chkStartAfterStep.TabIndex = 10
        Me.chkStartAfterStep.Text = "Start recording at step #"
        Me.chkStartAfterStep.UseVisualStyleBackColor = True
        '
        'Label19
        '
        Me.Label19.AutoSize = True
        Me.Label19.Location = New System.Drawing.Point(438, 111)
        Me.Label19.Name = "Label19"
        Me.Label19.Size = New System.Drawing.Size(33, 16)
        Me.Label19.TabIndex = 2
        Me.Label19.Text = "Max"
        '
        'Label18
        '
        Me.Label18.AutoSize = True
        Me.Label18.Location = New System.Drawing.Point(378, 111)
        Me.Label18.Name = "Label18"
        Me.Label18.Size = New System.Drawing.Size(29, 16)
        Me.Label18.TabIndex = 2
        Me.Label18.Text = "Min"
        '
        'txtMaxScaleY
        '
        Me.txtMaxScaleY.Location = New System.Drawing.Point(431, 127)
        Me.txtMaxScaleY.Name = "txtMaxScaleY"
        Me.txtMaxScaleY.Size = New System.Drawing.Size(52, 22)
        Me.txtMaxScaleY.TabIndex = 8
        Me.txtMaxScaleY.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        '
        'txtMinScaleY
        '
        Me.txtMinScaleY.Location = New System.Drawing.Point(371, 127)
        Me.txtMinScaleY.Name = "txtMinScaleY"
        Me.txtMinScaleY.Size = New System.Drawing.Size(52, 22)
        Me.txtMinScaleY.TabIndex = 8
        Me.txtMinScaleY.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        '
        'radFullScale
        '
        Me.radFullScale.AutoSize = True
        Me.radFullScale.Location = New System.Drawing.Point(379, 24)
        Me.radFullScale.Name = "radFullScale"
        Me.radFullScale.Size = New System.Drawing.Size(83, 20)
        Me.radFullScale.TabIndex = 7
        Me.radFullScale.TabStop = True
        Me.radFullScale.Text = "Full scale"
        Me.radFullScale.UseVisualStyleBackColor = True
        '
        'radRunningScale
        '
        Me.radRunningScale.AutoSize = True
        Me.radRunningScale.Location = New System.Drawing.Point(262, 24)
        Me.radRunningScale.Name = "radRunningScale"
        Me.radRunningScale.Size = New System.Drawing.Size(111, 20)
        Me.radRunningScale.TabIndex = 7
        Me.radRunningScale.TabStop = True
        Me.radRunningScale.Text = "Running scale"
        Me.radRunningScale.UseVisualStyleBackColor = True
        '
        'chkAutoScaleY
        '
        Me.chkAutoScaleY.AutoSize = True
        Me.chkAutoScaleY.Location = New System.Drawing.Point(262, 127)
        Me.chkAutoScaleY.Name = "chkAutoScaleY"
        Me.chkAutoScaleY.Size = New System.Drawing.Size(110, 20)
        Me.chkAutoScaleY.TabIndex = 6
        Me.chkAutoScaleY.Text = "Auto   Scale Y"
        Me.chkAutoScaleY.UseVisualStyleBackColor = True
        '
        'chkAutoScaleX
        '
        Me.chkAutoScaleX.AutoSize = True
        Me.chkAutoScaleX.Location = New System.Drawing.Point(262, 91)
        Me.chkAutoScaleX.Name = "chkAutoScaleX"
        Me.chkAutoScaleX.Size = New System.Drawing.Size(101, 20)
        Me.chkAutoScaleX.TabIndex = 6
        Me.chkAutoScaleX.Text = "Auto scale X"
        Me.chkAutoScaleX.UseVisualStyleBackColor = True
        '
        'numBPosY
        '
        Me.numBPosY.Location = New System.Drawing.Point(326, 234)
        Me.numBPosY.Name = "numBPosY"
        Me.numBPosY.Size = New System.Drawing.Size(65, 22)
        Me.numBPosY.TabIndex = 5
        Me.numBPosY.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numAPosY
        '
        Me.numAPosY.Location = New System.Drawing.Point(126, 234)
        Me.numAPosY.Name = "numAPosY"
        Me.numAPosY.Size = New System.Drawing.Size(65, 22)
        Me.numAPosY.TabIndex = 5
        Me.numAPosY.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numBPosX
        '
        Me.numBPosX.Location = New System.Drawing.Point(238, 234)
        Me.numBPosX.Name = "numBPosX"
        Me.numBPosX.Size = New System.Drawing.Size(65, 22)
        Me.numBPosX.TabIndex = 5
        Me.numBPosX.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'numAPosX
        '
        Me.numAPosX.Location = New System.Drawing.Point(38, 234)
        Me.numAPosX.Name = "numAPosX"
        Me.numAPosX.Size = New System.Drawing.Size(65, 22)
        Me.numAPosX.TabIndex = 5
        Me.numAPosX.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'butPicPositions
        '
        Me.butPicPositions.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.butPicPositions.Location = New System.Drawing.Point(402, 226)
        Me.butPicPositions.Name = "butPicPositions"
        Me.butPicPositions.Size = New System.Drawing.Size(90, 34)
        Me.butPicPositions.TabIndex = 4
        Me.butPicPositions.Text = "Pick "
        Me.butPicPositions.UseVisualStyleBackColor = True
        '
        'Label16
        '
        Me.Label16.AutoSize = True
        Me.Label16.Location = New System.Drawing.Point(311, 236)
        Me.Label16.Name = "Label16"
        Me.Label16.Size = New System.Drawing.Size(17, 16)
        Me.Label16.TabIndex = 2
        Me.Label16.Text = "Y"
        '
        'cmbPlaceOn
        '
        Me.cmbPlaceOn.FormattingEnabled = True
        Me.cmbPlaceOn.Items.AddRange(New Object() {"Field", "Intensity"})
        Me.cmbPlaceOn.Location = New System.Drawing.Point(106, 24)
        Me.cmbPlaceOn.Name = "cmbPlaceOn"
        Me.cmbPlaceOn.Size = New System.Drawing.Size(130, 24)
        Me.cmbPlaceOn.TabIndex = 3
        '
        'cmbMeasurement
        '
        Me.cmbMeasurement.FormattingEnabled = True
        Me.cmbMeasurement.Items.AddRange(New Object() {"Amplitude", "Intensity", "Integrate Intensity"})
        Me.cmbMeasurement.Location = New System.Drawing.Point(106, 56)
        Me.cmbMeasurement.Name = "cmbMeasurement"
        Me.cmbMeasurement.Size = New System.Drawing.Size(130, 24)
        Me.cmbMeasurement.TabIndex = 3
        '
        'Label13
        '
        Me.Label13.AutoSize = True
        Me.Label13.Location = New System.Drawing.Point(111, 236)
        Me.Label13.Name = "Label13"
        Me.Label13.Size = New System.Drawing.Size(17, 16)
        Me.Label13.TabIndex = 2
        Me.Label13.Text = "Y"
        '
        'Label15
        '
        Me.Label15.AutoSize = True
        Me.Label15.Location = New System.Drawing.Point(223, 236)
        Me.Label15.Name = "Label15"
        Me.Label15.Size = New System.Drawing.Size(16, 16)
        Me.Label15.TabIndex = 2
        Me.Label15.Text = "X"
        '
        'cmbBoxSize
        '
        Me.cmbBoxSize.FormattingEnabled = True
        Me.cmbBoxSize.Items.AddRange(New Object() {"Tiny (200 x100)", "Small (300 x 150)", "Medium (400x200)", "Large (500x250)", "Extra Large (700x350)"})
        Me.cmbBoxSize.Location = New System.Drawing.Point(106, 124)
        Me.cmbBoxSize.Name = "cmbBoxSize"
        Me.cmbBoxSize.Size = New System.Drawing.Size(130, 24)
        Me.cmbBoxSize.TabIndex = 3
        '
        'cmbProbeType
        '
        Me.cmbProbeType.FormattingEnabled = True
        Me.cmbProbeType.Items.AddRange(New Object() {"Point (A)", "Line (A-B)"})
        Me.cmbProbeType.Location = New System.Drawing.Point(106, 89)
        Me.cmbProbeType.Name = "cmbProbeType"
        Me.cmbProbeType.Size = New System.Drawing.Size(130, 24)
        Me.cmbProbeType.TabIndex = 3
        '
        'Label14
        '
        Me.Label14.AutoSize = True
        Me.Label14.Location = New System.Drawing.Point(277, 209)
        Me.Label14.Name = "Label14"
        Me.Label14.Size = New System.Drawing.Size(68, 16)
        Me.Label14.TabIndex = 2
        Me.Label14.Text = "Position B"
        '
        'Label12
        '
        Me.Label12.AutoSize = True
        Me.Label12.Location = New System.Drawing.Point(23, 236)
        Me.Label12.Name = "Label12"
        Me.Label12.Size = New System.Drawing.Size(16, 16)
        Me.Label12.TabIndex = 2
        Me.Label12.Text = "X"
        '
        'Label11
        '
        Me.Label11.AutoSize = True
        Me.Label11.Location = New System.Drawing.Point(76, 209)
        Me.Label11.Name = "Label11"
        Me.Label11.Size = New System.Drawing.Size(68, 16)
        Me.Label11.TabIndex = 2
        Me.Label11.Text = "Position A"
        '
        'Label17
        '
        Me.Label17.AutoSize = True
        Me.Label17.Location = New System.Drawing.Point(14, 127)
        Me.Label17.Name = "Label17"
        Me.Label17.Size = New System.Drawing.Size(60, 16)
        Me.Label17.TabIndex = 2
        Me.Label17.Text = "Box Size"
        '
        'Label10
        '
        Me.Label10.AutoSize = True
        Me.Label10.Location = New System.Drawing.Point(14, 92)
        Me.Label10.Name = "Label10"
        Me.Label10.Size = New System.Drawing.Size(74, 16)
        Me.Label10.TabIndex = 2
        Me.Label10.Text = "Probe type"
        '
        'Label9
        '
        Me.Label9.AutoSize = True
        Me.Label9.Location = New System.Drawing.Point(12, 59)
        Me.Label9.Name = "Label9"
        Me.Label9.Size = New System.Drawing.Size(90, 16)
        Me.Label9.TabIndex = 2
        Me.Label9.Text = "Measurement"
        '
        'Label8
        '
        Me.Label8.AutoSize = True
        Me.Label8.Location = New System.Drawing.Point(14, 29)
        Me.Label8.Name = "Label8"
        Me.Label8.Size = New System.Drawing.Size(64, 16)
        Me.Label8.TabIndex = 1
        Me.Label8.Text = "Place on:"
        '
        'pnlProbeImage
        '
        Me.pnlProbeImage.Location = New System.Drawing.Point(16, 14)
        Me.pnlProbeImage.Name = "pnlProbeImage"
        Me.pnlProbeImage.Size = New System.Drawing.Size(1160, 653)
        Me.pnlProbeImage.TabIndex = 0
        '
        'tabViewer
        '
        Me.tabViewer.Controls.Add(Me.pnlPreviewIntensityProps)
        Me.tabViewer.Controls.Add(Me.Panel3)
        Me.tabViewer.Controls.Add(Me.Label56)
        Me.tabViewer.Controls.Add(Me.Label55)
        Me.tabViewer.Controls.Add(Me.lblWarningIntensity)
        Me.tabViewer.Controls.Add(Me.lblWarningField)
        Me.tabViewer.Controls.Add(Me.btnExportIntensityFrame)
        Me.tabViewer.Controls.Add(Me.btnExportFieldFrame)
        Me.tabViewer.Controls.Add(Me.btnOpenImageFolder)
        Me.tabViewer.Controls.Add(Me.btnOpenVideoFolder)
        Me.tabViewer.Controls.Add(Me.btnViewIntensityVideo)
        Me.tabViewer.Controls.Add(Me.btnViewFieldVideo)
        Me.tabViewer.Controls.Add(Me.pnlPreviewIntensity)
        Me.tabViewer.Controls.Add(Me.pnlPreviewField)
        Me.tabViewer.Location = New System.Drawing.Point(4, 34)
        Me.tabViewer.Name = "tabViewer"
        Me.tabViewer.Size = New System.Drawing.Size(1742, 693)
        Me.tabViewer.TabIndex = 4
        Me.tabViewer.Text = "     Preview     "
        Me.tabViewer.UseVisualStyleBackColor = True
        '
        'pnlPreviewIntensityProps
        '
        Me.pnlPreviewIntensityProps.BackColor = System.Drawing.SystemColors.ButtonFace
        Me.pnlPreviewIntensityProps.Controls.Add(Me.chkExportVideoIntensity2)
        Me.pnlPreviewIntensityProps.Controls.Add(Me.Label64)
        Me.pnlPreviewIntensityProps.Controls.Add(Me.numBrightnessIntensityOverlay)
        Me.pnlPreviewIntensityProps.Controls.Add(Me.numIntensityScaling2)
        Me.pnlPreviewIntensityProps.Controls.Add(Me.chkDiplayOverlayInIntensity)
        Me.pnlPreviewIntensityProps.Controls.Add(Me.Label66)
        Me.pnlPreviewIntensityProps.Location = New System.Drawing.Point(1023, 12)
        Me.pnlPreviewIntensityProps.Name = "pnlPreviewIntensityProps"
        Me.pnlPreviewIntensityProps.Size = New System.Drawing.Size(700, 40)
        Me.pnlPreviewIntensityProps.TabIndex = 14
        '
        'chkExportVideoIntensity2
        '
        Me.chkExportVideoIntensity2.AutoSize = True
        Me.chkExportVideoIntensity2.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkExportVideoIntensity2.Location = New System.Drawing.Point(577, 8)
        Me.chkExportVideoIntensity2.Name = "chkExportVideoIntensity2"
        Me.chkExportVideoIntensity2.Size = New System.Drawing.Size(115, 24)
        Me.chkExportVideoIntensity2.TabIndex = 12
        Me.chkExportVideoIntensity2.Text = "Export video"
        Me.chkExportVideoIntensity2.UseVisualStyleBackColor = True
        '
        'Label64
        '
        Me.Label64.AutoSize = True
        Me.Label64.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label64.Location = New System.Drawing.Point(13, 9)
        Me.Label64.Name = "Label64"
        Me.Label64.Size = New System.Drawing.Size(129, 20)
        Me.Label64.TabIndex = 9
        Me.Label64.Text = "Field scale factor"
        '
        'numBrightnessIntensityOverlay
        '
        Me.numBrightnessIntensityOverlay.DecimalPlaces = 2
        Me.numBrightnessIntensityOverlay.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numBrightnessIntensityOverlay.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        Me.numBrightnessIntensityOverlay.Location = New System.Drawing.Point(473, 7)
        Me.numBrightnessIntensityOverlay.Maximum = New Decimal(New Integer() {5, 0, 0, 0})
        Me.numBrightnessIntensityOverlay.Name = "numBrightnessIntensityOverlay"
        Me.numBrightnessIntensityOverlay.Size = New System.Drawing.Size(67, 26)
        Me.numBrightnessIntensityOverlay.TabIndex = 11
        Me.numBrightnessIntensityOverlay.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numBrightnessIntensityOverlay.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'numIntensityScaling2
        '
        Me.numIntensityScaling2.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numIntensityScaling2.Location = New System.Drawing.Point(151, 7)
        Me.numIntensityScaling2.Maximum = New Decimal(New Integer() {1000, 0, 0, 0})
        Me.numIntensityScaling2.Name = "numIntensityScaling2"
        Me.numIntensityScaling2.Size = New System.Drawing.Size(74, 26)
        Me.numIntensityScaling2.TabIndex = 8
        Me.numIntensityScaling2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'chkDiplayOverlayInIntensity
        '
        Me.chkDiplayOverlayInIntensity.AutoSize = True
        Me.chkDiplayOverlayInIntensity.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkDiplayOverlayInIntensity.Location = New System.Drawing.Point(253, 9)
        Me.chkDiplayOverlayInIntensity.Name = "chkDiplayOverlayInIntensity"
        Me.chkDiplayOverlayInIntensity.Size = New System.Drawing.Size(132, 24)
        Me.chkDiplayOverlayInIntensity.TabIndex = 10
        Me.chkDiplayOverlayInIntensity.Text = "Display overlay"
        Me.chkDiplayOverlayInIntensity.UseVisualStyleBackColor = True
        '
        'Label66
        '
        Me.Label66.AutoSize = True
        Me.Label66.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label66.Location = New System.Drawing.Point(382, 10)
        Me.Label66.Name = "Label66"
        Me.Label66.Size = New System.Drawing.Size(91, 20)
        Me.Label66.TabIndex = 9
        Me.Label66.Text = "/ brightness"
        '
        'Panel3
        '
        Me.Panel3.BackColor = System.Drawing.SystemColors.ButtonFace
        Me.Panel3.Controls.Add(Me.Label63)
        Me.Panel3.Controls.Add(Me.chkExportVideoField2)
        Me.Panel3.Controls.Add(Me.numFieldScaling2)
        Me.Panel3.Controls.Add(Me.Label65)
        Me.Panel3.Controls.Add(Me.numBrightnessFieldOverlay)
        Me.Panel3.Controls.Add(Me.chkDiplayOverlayInField)
        Me.Panel3.Location = New System.Drawing.Point(170, 11)
        Me.Panel3.Name = "Panel3"
        Me.Panel3.Size = New System.Drawing.Size(697, 40)
        Me.Panel3.TabIndex = 13
        '
        'Label63
        '
        Me.Label63.AutoSize = True
        Me.Label63.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label63.Location = New System.Drawing.Point(9, 10)
        Me.Label63.Name = "Label63"
        Me.Label63.Size = New System.Drawing.Size(129, 20)
        Me.Label63.TabIndex = 9
        Me.Label63.Text = "Field scale factor"
        '
        'chkExportVideoField2
        '
        Me.chkExportVideoField2.AutoSize = True
        Me.chkExportVideoField2.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkExportVideoField2.Location = New System.Drawing.Point(571, 9)
        Me.chkExportVideoField2.Name = "chkExportVideoField2"
        Me.chkExportVideoField2.Size = New System.Drawing.Size(115, 24)
        Me.chkExportVideoField2.TabIndex = 12
        Me.chkExportVideoField2.Text = "Export video"
        Me.chkExportVideoField2.UseVisualStyleBackColor = True
        '
        'numFieldScaling2
        '
        Me.numFieldScaling2.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numFieldScaling2.Location = New System.Drawing.Point(144, 8)
        Me.numFieldScaling2.Maximum = New Decimal(New Integer() {1000, 0, 0, 0})
        Me.numFieldScaling2.Name = "numFieldScaling2"
        Me.numFieldScaling2.Size = New System.Drawing.Size(75, 26)
        Me.numFieldScaling2.TabIndex = 8
        Me.numFieldScaling2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'Label65
        '
        Me.Label65.AutoSize = True
        Me.Label65.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label65.Location = New System.Drawing.Point(380, 10)
        Me.Label65.Name = "Label65"
        Me.Label65.Size = New System.Drawing.Size(91, 20)
        Me.Label65.TabIndex = 9
        Me.Label65.Text = "/ brightness"
        '
        'numBrightnessFieldOverlay
        '
        Me.numBrightnessFieldOverlay.DecimalPlaces = 2
        Me.numBrightnessFieldOverlay.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.numBrightnessFieldOverlay.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        Me.numBrightnessFieldOverlay.Location = New System.Drawing.Point(474, 8)
        Me.numBrightnessFieldOverlay.Maximum = New Decimal(New Integer() {5, 0, 0, 0})
        Me.numBrightnessFieldOverlay.Name = "numBrightnessFieldOverlay"
        Me.numBrightnessFieldOverlay.Size = New System.Drawing.Size(67, 26)
        Me.numBrightnessFieldOverlay.TabIndex = 11
        Me.numBrightnessFieldOverlay.TextAlign = System.Windows.Forms.HorizontalAlignment.Center
        Me.numBrightnessFieldOverlay.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'chkDiplayOverlayInField
        '
        Me.chkDiplayOverlayInField.AutoSize = True
        Me.chkDiplayOverlayInField.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.chkDiplayOverlayInField.Location = New System.Drawing.Point(254, 9)
        Me.chkDiplayOverlayInField.Name = "chkDiplayOverlayInField"
        Me.chkDiplayOverlayInField.Size = New System.Drawing.Size(132, 24)
        Me.chkDiplayOverlayInField.TabIndex = 10
        Me.chkDiplayOverlayInField.Text = "Display overlay"
        Me.chkDiplayOverlayInField.UseVisualStyleBackColor = True
        '
        'Label56
        '
        Me.Label56.AutoSize = True
        Me.Label56.Font = New System.Drawing.Font("Microsoft Sans Serif", 20.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label56.Location = New System.Drawing.Point(881, 16)
        Me.Label56.Name = "Label56"
        Me.Label56.Size = New System.Drawing.Size(126, 31)
        Me.Label56.TabIndex = 7
        Me.Label56.Text = "Intensity"
        '
        'Label55
        '
        Me.Label55.AutoSize = True
        Me.Label55.Font = New System.Drawing.Font("Microsoft Sans Serif", 20.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label55.Location = New System.Drawing.Point(53, 16)
        Me.Label55.Name = "Label55"
        Me.Label55.Size = New System.Drawing.Size(78, 31)
        Me.Label55.TabIndex = 7
        Me.Label55.Text = "Field"
        '
        'lblWarningIntensity
        '
        Me.lblWarningIntensity.AutoSize = True
        Me.lblWarningIntensity.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblWarningIntensity.Location = New System.Drawing.Point(1201, 550)
        Me.lblWarningIntensity.Name = "lblWarningIntensity"
        Me.lblWarningIntensity.Size = New System.Drawing.Size(183, 20)
        Me.lblWarningIntensity.TabIndex = 6
        Me.lblWarningIntensity.Text = "Intensity video is created"
        '
        'lblWarningField
        '
        Me.lblWarningField.AutoSize = True
        Me.lblWarningField.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblWarningField.Location = New System.Drawing.Point(335, 551)
        Me.lblWarningField.Name = "lblWarningField"
        Me.lblWarningField.Size = New System.Drawing.Size(157, 20)
        Me.lblWarningField.TabIndex = 6
        Me.lblWarningField.Text = "Field video is created"
        '
        'btnExportIntensityFrame
        '
        Me.btnExportIntensityFrame.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnExportIntensityFrame.Location = New System.Drawing.Point(1410, 545)
        Me.btnExportIntensityFrame.Name = "btnExportIntensityFrame"
        Me.btnExportIntensityFrame.Size = New System.Drawing.Size(247, 31)
        Me.btnExportIntensityFrame.TabIndex = 5
        Me.btnExportIntensityFrame.Text = "Export frame as image"
        Me.btnExportIntensityFrame.UseVisualStyleBackColor = True
        '
        'btnExportFieldFrame
        '
        Me.btnExportFieldFrame.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnExportFieldFrame.Location = New System.Drawing.Point(536, 547)
        Me.btnExportFieldFrame.Name = "btnExportFieldFrame"
        Me.btnExportFieldFrame.Size = New System.Drawing.Size(243, 29)
        Me.btnExportFieldFrame.TabIndex = 4
        Me.btnExportFieldFrame.Text = "Export frame as Image"
        Me.btnExportFieldFrame.UseVisualStyleBackColor = True
        '
        'btnOpenImageFolder
        '
        Me.btnOpenImageFolder.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnOpenImageFolder.Location = New System.Drawing.Point(1524, 650)
        Me.btnOpenImageFolder.Name = "btnOpenImageFolder"
        Me.btnOpenImageFolder.Size = New System.Drawing.Size(207, 33)
        Me.btnOpenImageFolder.TabIndex = 2
        Me.btnOpenImageFolder.Text = "Open Image folder"
        Me.btnOpenImageFolder.UseVisualStyleBackColor = True
        '
        'btnOpenVideoFolder
        '
        Me.btnOpenVideoFolder.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnOpenVideoFolder.Location = New System.Drawing.Point(1311, 650)
        Me.btnOpenVideoFolder.Name = "btnOpenVideoFolder"
        Me.btnOpenVideoFolder.Size = New System.Drawing.Size(207, 33)
        Me.btnOpenVideoFolder.TabIndex = 2
        Me.btnOpenVideoFolder.Text = "Open video folder"
        Me.btnOpenVideoFolder.UseVisualStyleBackColor = True
        '
        'btnViewIntensityVideo
        '
        Me.btnViewIntensityVideo.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnViewIntensityVideo.Location = New System.Drawing.Point(929, 546)
        Me.btnViewIntensityVideo.Name = "btnViewIntensityVideo"
        Me.btnViewIntensityVideo.Size = New System.Drawing.Size(247, 29)
        Me.btnViewIntensityVideo.TabIndex = 1
        Me.btnViewIntensityVideo.Text = "View video in media player"
        Me.btnViewIntensityVideo.UseVisualStyleBackColor = True
        '
        'btnViewFieldVideo
        '
        Me.btnViewFieldVideo.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnViewFieldVideo.Location = New System.Drawing.Point(79, 547)
        Me.btnViewFieldVideo.Name = "btnViewFieldVideo"
        Me.btnViewFieldVideo.Size = New System.Drawing.Size(216, 29)
        Me.btnViewFieldVideo.TabIndex = 1
        Me.btnViewFieldVideo.Text = "View video in media player"
        Me.btnViewFieldVideo.UseVisualStyleBackColor = True
        '
        'pnlPreviewIntensity
        '
        Me.pnlPreviewIntensity.Location = New System.Drawing.Point(873, 57)
        Me.pnlPreviewIntensity.Name = "pnlPreviewIntensity"
        Me.pnlPreviewIntensity.Size = New System.Drawing.Size(850, 478)
        Me.pnlPreviewIntensity.TabIndex = 0
        '
        'pnlPreviewField
        '
        Me.pnlPreviewField.Location = New System.Drawing.Point(17, 56)
        Me.pnlPreviewField.Name = "pnlPreviewField"
        Me.pnlPreviewField.Size = New System.Drawing.Size(850, 478)
        Me.pnlPreviewField.TabIndex = 0
        '
        'tabOutput
        '
        Me.tabOutput.Controls.Add(Me.btnRenameVideo)
        Me.tabOutput.Controls.Add(Me.btnShareWithEditors)
        Me.tabOutput.Controls.Add(Me.btnCopyToClipboard)
        Me.tabOutput.Controls.Add(Me.btnOpenImageFolder2)
        Me.tabOutput.Controls.Add(Me.btnViewVIdeoInPlayer2)
        Me.tabOutput.Controls.Add(Me.btnOpenVIdeoFolder2)
        Me.tabOutput.Controls.Add(Me.lblPreview)
        Me.tabOutput.Controls.Add(Me.btnDeleteVideos)
        Me.tabOutput.Controls.Add(Me.btnDeleteImages)
        Me.tabOutput.Controls.Add(Me.btnDeleteScene)
        Me.tabOutput.Controls.Add(Me.btnLoadExternal)
        Me.tabOutput.Controls.Add(Me.pnlPreview)
        Me.tabOutput.Controls.Add(Me.Label57)
        Me.tabOutput.Controls.Add(Me.lstScenes)
        Me.tabOutput.Controls.Add(Me.lstImages)
        Me.tabOutput.Controls.Add(Me.lstVideo)
        Me.tabOutput.Controls.Add(Me.lstAllProjects3)
        Me.tabOutput.Location = New System.Drawing.Point(4, 34)
        Me.tabOutput.Name = "tabOutput"
        Me.tabOutput.Size = New System.Drawing.Size(1742, 693)
        Me.tabOutput.TabIndex = 3
        Me.tabOutput.Text = "  Manage output  "
        Me.tabOutput.UseVisualStyleBackColor = True
        '
        'btnRenameVideo
        '
        Me.btnRenameVideo.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnRenameVideo.Location = New System.Drawing.Point(566, 631)
        Me.btnRenameVideo.Name = "btnRenameVideo"
        Me.btnRenameVideo.Size = New System.Drawing.Size(75, 31)
        Me.btnRenameVideo.TabIndex = 20
        Me.btnRenameVideo.Text = "Rename"
        Me.btnRenameVideo.UseVisualStyleBackColor = True
        '
        'btnShareWithEditors
        '
        Me.btnShareWithEditors.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnShareWithEditors.Location = New System.Drawing.Point(1517, 635)
        Me.btnShareWithEditors.Name = "btnShareWithEditors"
        Me.btnShareWithEditors.Size = New System.Drawing.Size(193, 27)
        Me.btnShareWithEditors.TabIndex = 19
        Me.btnShareWithEditors.Text = "Share this image with tabs"
        Me.btnShareWithEditors.UseVisualStyleBackColor = True
        '
        'btnCopyToClipboard
        '
        Me.btnCopyToClipboard.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnCopyToClipboard.Location = New System.Drawing.Point(594, 407)
        Me.btnCopyToClipboard.Name = "btnCopyToClipboard"
        Me.btnCopyToClipboard.Size = New System.Drawing.Size(129, 30)
        Me.btnCopyToClipboard.TabIndex = 18
        Me.btnCopyToClipboard.Text = "Copy to clipboard"
        Me.btnCopyToClipboard.UseVisualStyleBackColor = True
        '
        'btnOpenImageFolder2
        '
        Me.btnOpenImageFolder2.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnOpenImageFolder2.Location = New System.Drawing.Point(759, 407)
        Me.btnOpenImageFolder2.Name = "btnOpenImageFolder2"
        Me.btnOpenImageFolder2.Size = New System.Drawing.Size(132, 30)
        Me.btnOpenImageFolder2.TabIndex = 17
        Me.btnOpenImageFolder2.Text = "Open image folder"
        Me.btnOpenImageFolder2.UseVisualStyleBackColor = True
        '
        'btnViewVIdeoInPlayer2
        '
        Me.btnViewVIdeoInPlayer2.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnViewVIdeoInPlayer2.Location = New System.Drawing.Point(677, 630)
        Me.btnViewVIdeoInPlayer2.Name = "btnViewVIdeoInPlayer2"
        Me.btnViewVIdeoInPlayer2.Size = New System.Drawing.Size(114, 33)
        Me.btnViewVIdeoInPlayer2.TabIndex = 16
        Me.btnViewVIdeoInPlayer2.Text = "View in player"
        Me.btnViewVIdeoInPlayer2.UseVisualStyleBackColor = True
        '
        'btnOpenVIdeoFolder2
        '
        Me.btnOpenVIdeoFolder2.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnOpenVIdeoFolder2.Location = New System.Drawing.Point(799, 630)
        Me.btnOpenVIdeoFolder2.Name = "btnOpenVIdeoFolder2"
        Me.btnOpenVIdeoFolder2.Size = New System.Drawing.Size(93, 34)
        Me.btnOpenVIdeoFolder2.TabIndex = 15
        Me.btnOpenVIdeoFolder2.Text = "Open folder"
        Me.btnOpenVIdeoFolder2.UseVisualStyleBackColor = True
        '
        'lblPreview
        '
        Me.lblPreview.AutoSize = True
        Me.lblPreview.Location = New System.Drawing.Point(911, 25)
        Me.lblPreview.Name = "lblPreview"
        Me.lblPreview.Size = New System.Drawing.Size(88, 25)
        Me.lblPreview.TabIndex = 14
        Me.lblPreview.Text = "Preview"
        '
        'btnDeleteVideos
        '
        Me.btnDeleteVideos.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnDeleteVideos.Location = New System.Drawing.Point(456, 631)
        Me.btnDeleteVideos.Name = "btnDeleteVideos"
        Me.btnDeleteVideos.Size = New System.Drawing.Size(101, 31)
        Me.btnDeleteVideos.TabIndex = 13
        Me.btnDeleteVideos.Text = "Delete video"
        Me.btnDeleteVideos.UseVisualStyleBackColor = True
        '
        'btnDeleteImages
        '
        Me.btnDeleteImages.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnDeleteImages.Location = New System.Drawing.Point(458, 405)
        Me.btnDeleteImages.Name = "btnDeleteImages"
        Me.btnDeleteImages.Size = New System.Drawing.Size(99, 32)
        Me.btnDeleteImages.TabIndex = 13
        Me.btnDeleteImages.Text = "Delete image"
        Me.btnDeleteImages.UseVisualStyleBackColor = True
        '
        'btnDeleteScene
        '
        Me.btnDeleteScene.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnDeleteScene.Location = New System.Drawing.Point(459, 177)
        Me.btnDeleteScene.Name = "btnDeleteScene"
        Me.btnDeleteScene.Size = New System.Drawing.Size(111, 32)
        Me.btnDeleteScene.TabIndex = 13
        Me.btnDeleteScene.Text = "Delete scene"
        Me.btnDeleteScene.UseVisualStyleBackColor = True
        '
        'btnLoadExternal
        '
        Me.btnLoadExternal.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnLoadExternal.Location = New System.Drawing.Point(785, 177)
        Me.btnLoadExternal.Name = "btnLoadExternal"
        Me.btnLoadExternal.Size = New System.Drawing.Size(104, 32)
        Me.btnLoadExternal.TabIndex = 13
        Me.btnLoadExternal.Text = "Load external"
        Me.btnLoadExternal.UseVisualStyleBackColor = True
        '
        'pnlPreview
        '
        Me.pnlPreview.BackColor = System.Drawing.Color.Gainsboro
        Me.pnlPreview.Location = New System.Drawing.Point(908, 52)
        Me.pnlPreview.Name = "pnlPreview"
        Me.pnlPreview.Size = New System.Drawing.Size(806, 573)
        Me.pnlPreview.TabIndex = 12
        '
        'Label57
        '
        Me.Label57.AutoSize = True
        Me.Label57.Location = New System.Drawing.Point(20, 16)
        Me.Label57.Name = "Label57"
        Me.Label57.Size = New System.Drawing.Size(227, 25)
        Me.Label57.TabIndex = 11
        Me.Label57.Text = "Manage project output"
        '
        'lstScenes
        '
        Me.lstScenes.BackColor = System.Drawing.SystemColors.MenuBar
        Me.lstScenes.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.colSceneName, Me.colSceneCreated})
        Me.lstScenes.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lstScenes.FullRowSelect = True
        Me.lstScenes.HideSelection = False
        Me.lstScenes.Location = New System.Drawing.Point(460, 51)
        Me.lstScenes.Name = "lstScenes"
        Me.lstScenes.Size = New System.Drawing.Size(432, 126)
        Me.lstScenes.TabIndex = 10
        Me.lstScenes.UseCompatibleStateImageBehavior = False
        Me.lstScenes.View = System.Windows.Forms.View.Details
        '
        'colSceneName
        '
        Me.colSceneName.Text = "Scene file name"
        Me.colSceneName.Width = 262
        '
        'colSceneCreated
        '
        Me.colSceneCreated.Text = "Created on"
        Me.colSceneCreated.Width = 138
        '
        'lstImages
        '
        Me.lstImages.BackColor = System.Drawing.SystemColors.Menu
        Me.lstImages.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.colImageName, Me.colCreatedOn})
        Me.lstImages.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lstImages.FullRowSelect = True
        Me.lstImages.HideSelection = False
        Me.lstImages.Location = New System.Drawing.Point(460, 221)
        Me.lstImages.Name = "lstImages"
        Me.lstImages.Size = New System.Drawing.Size(432, 183)
        Me.lstImages.TabIndex = 9
        Me.lstImages.UseCompatibleStateImageBehavior = False
        Me.lstImages.View = System.Windows.Forms.View.Details
        '
        'colImageName
        '
        Me.colImageName.Text = "Image file name"
        Me.colImageName.Width = 258
        '
        'colCreatedOn
        '
        Me.colCreatedOn.Text = "Created on"
        Me.colCreatedOn.Width = 141
        '
        'lstVideo
        '
        Me.lstVideo.BackColor = System.Drawing.SystemColors.Menu
        Me.lstVideo.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.colVideoName, Me.colCreatedOn2})
        Me.lstVideo.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lstVideo.FullRowSelect = True
        Me.lstVideo.HideSelection = False
        Me.lstVideo.Location = New System.Drawing.Point(457, 452)
        Me.lstVideo.Name = "lstVideo"
        Me.lstVideo.Size = New System.Drawing.Size(435, 173)
        Me.lstVideo.TabIndex = 9
        Me.lstVideo.UseCompatibleStateImageBehavior = False
        Me.lstVideo.View = System.Windows.Forms.View.Details
        '
        'colVideoName
        '
        Me.colVideoName.Text = "Video file name"
        Me.colVideoName.Width = 255
        '
        'colCreatedOn2
        '
        Me.colCreatedOn2.Text = "Created on"
        Me.colCreatedOn2.Width = 145
        '
        'lstAllProjects3
        '
        Me.lstAllProjects3.BackColor = System.Drawing.SystemColors.Menu
        Me.lstAllProjects3.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.colProjectName2, Me.colLastEdited2})
        Me.lstAllProjects3.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lstAllProjects3.FullRowSelect = True
        Me.lstAllProjects3.HideSelection = False
        Me.lstAllProjects3.Location = New System.Drawing.Point(18, 51)
        Me.lstAllProjects3.MultiSelect = False
        Me.lstAllProjects3.Name = "lstAllProjects3"
        Me.lstAllProjects3.Size = New System.Drawing.Size(427, 611)
        Me.lstAllProjects3.TabIndex = 9
        Me.lstAllProjects3.UseCompatibleStateImageBehavior = False
        Me.lstAllProjects3.View = System.Windows.Forms.View.Details
        '
        'colProjectName2
        '
        Me.colProjectName2.Text = "Project name"
        Me.colProjectName2.Width = 280
        '
        'colLastEdited2
        '
        Me.colLastEdited2.Text = "Last edited"
        Me.colLastEdited2.Width = 160
        '
        'tabColorScheme
        '
        Me.tabColorScheme.Controls.Add(Me.lblType)
        Me.tabColorScheme.Controls.Add(Me.radSchemeTypeIntensity)
        Me.tabColorScheme.Controls.Add(Me.radSchemeTypeField)
        Me.tabColorScheme.Controls.Add(Me.btnLoadScheme)
        Me.tabColorScheme.Controls.Add(Me.btbChooseFFolder)
        Me.tabColorScheme.Controls.Add(Me.lblSchemeFolder)
        Me.tabColorScheme.Controls.Add(Me.ListView1)
        Me.tabColorScheme.Controls.Add(Me.txtSchemeFolder)
        Me.tabColorScheme.Controls.Add(Me.txtCurrentColorScheme)
        Me.tabColorScheme.Controls.Add(Me.lblCurrentCOlorScheme)
        Me.tabColorScheme.Controls.Add(Me.lblColorSchemeEditor)
        Me.tabColorScheme.Controls.Add(Me.btnColor9)
        Me.tabColorScheme.Controls.Add(Me.btnColor8)
        Me.tabColorScheme.Controls.Add(Me.btnColor7)
        Me.tabColorScheme.Controls.Add(Me.btnColor6)
        Me.tabColorScheme.Controls.Add(Me.btnColor5)
        Me.tabColorScheme.Controls.Add(Me.btnColor4)
        Me.tabColorScheme.Controls.Add(Me.btnColor3)
        Me.tabColorScheme.Controls.Add(Me.btnCOlor2)
        Me.tabColorScheme.Controls.Add(Me.btnColor1)
        Me.tabColorScheme.Controls.Add(Me.lblColorPicker)
        Me.tabColorScheme.Controls.Add(Me.Panel2)
        Me.tabColorScheme.Controls.Add(Me.pnlScheme)
        Me.tabColorScheme.Controls.Add(Me.btnDeleteColorScheme)
        Me.tabColorScheme.Controls.Add(Me.btnRenameColorScheme)
        Me.tabColorScheme.Controls.Add(Me.btnSaveAsColorScheme)
        Me.tabColorScheme.Controls.Add(Me.btnSaveCurrentScheme)
        Me.tabColorScheme.Location = New System.Drawing.Point(4, 34)
        Me.tabColorScheme.Name = "tabColorScheme"
        Me.tabColorScheme.Padding = New System.Windows.Forms.Padding(3)
        Me.tabColorScheme.Size = New System.Drawing.Size(1742, 693)
        Me.tabColorScheme.TabIndex = 0
        Me.tabColorScheme.Text = "      Colors      "
        Me.tabColorScheme.UseVisualStyleBackColor = True
        '
        'lblType
        '
        Me.lblType.AutoSize = True
        Me.lblType.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblType.Location = New System.Drawing.Point(23, 93)
        Me.lblType.Name = "lblType"
        Me.lblType.Size = New System.Drawing.Size(98, 18)
        Me.lblType.TabIndex = 19
        Me.lblType.Text = "Scheme type:"
        '
        'radSchemeTypeIntensity
        '
        Me.radSchemeTypeIntensity.AutoSize = True
        Me.radSchemeTypeIntensity.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.radSchemeTypeIntensity.Location = New System.Drawing.Point(197, 91)
        Me.radSchemeTypeIntensity.Name = "radSchemeTypeIntensity"
        Me.radSchemeTypeIntensity.Size = New System.Drawing.Size(79, 22)
        Me.radSchemeTypeIntensity.TabIndex = 18
        Me.radSchemeTypeIntensity.Text = "Intensity"
        Me.radSchemeTypeIntensity.UseVisualStyleBackColor = True
        '
        'radSchemeTypeField
        '
        Me.radSchemeTypeField.AutoSize = True
        Me.radSchemeTypeField.Checked = True
        Me.radSchemeTypeField.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.radSchemeTypeField.Location = New System.Drawing.Point(134, 91)
        Me.radSchemeTypeField.Name = "radSchemeTypeField"
        Me.radSchemeTypeField.Size = New System.Drawing.Size(57, 22)
        Me.radSchemeTypeField.TabIndex = 18
        Me.radSchemeTypeField.TabStop = True
        Me.radSchemeTypeField.Text = "Field"
        Me.radSchemeTypeField.UseVisualStyleBackColor = True
        '
        'btnLoadScheme
        '
        Me.btnLoadScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnLoadScheme.Location = New System.Drawing.Point(27, 528)
        Me.btnLoadScheme.Name = "btnLoadScheme"
        Me.btnLoadScheme.Size = New System.Drawing.Size(88, 28)
        Me.btnLoadScheme.TabIndex = 11
        Me.btnLoadScheme.Text = "Load "
        Me.btnLoadScheme.UseVisualStyleBackColor = True
        '
        'btbChooseFFolder
        '
        Me.btbChooseFFolder.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btbChooseFFolder.Location = New System.Drawing.Point(470, 125)
        Me.btbChooseFFolder.Name = "btbChooseFFolder"
        Me.btbChooseFFolder.Size = New System.Drawing.Size(89, 25)
        Me.btbChooseFFolder.TabIndex = 10
        Me.btbChooseFFolder.Text = "Choose..."
        Me.btbChooseFFolder.UseVisualStyleBackColor = True
        '
        'lblSchemeFolder
        '
        Me.lblSchemeFolder.AutoSize = True
        Me.lblSchemeFolder.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblSchemeFolder.Location = New System.Drawing.Point(24, 128)
        Me.lblSchemeFolder.Name = "lblSchemeFolder"
        Me.lblSchemeFolder.Size = New System.Drawing.Size(104, 18)
        Me.lblSchemeFolder.TabIndex = 9
        Me.lblSchemeFolder.Text = "Scheme folder"
        '
        'ListView1
        '
        Me.ListView1.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.ListView1.HideSelection = False
        Me.ListView1.Location = New System.Drawing.Point(27, 157)
        Me.ListView1.Name = "ListView1"
        Me.ListView1.Size = New System.Drawing.Size(531, 365)
        Me.ListView1.TabIndex = 8
        Me.ListView1.UseCompatibleStateImageBehavior = False
        '
        'txtSchemeFolder
        '
        Me.txtSchemeFolder.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtSchemeFolder.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.txtSchemeFolder.Location = New System.Drawing.Point(134, 125)
        Me.txtSchemeFolder.Name = "txtSchemeFolder"
        Me.txtSchemeFolder.Size = New System.Drawing.Size(330, 22)
        Me.txtSchemeFolder.TabIndex = 7
        '
        'txtCurrentColorScheme
        '
        Me.txtCurrentColorScheme.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtCurrentColorScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.txtCurrentColorScheme.Location = New System.Drawing.Point(135, 56)
        Me.txtCurrentColorScheme.Name = "txtCurrentColorScheme"
        Me.txtCurrentColorScheme.Size = New System.Drawing.Size(299, 24)
        Me.txtCurrentColorScheme.TabIndex = 7
        '
        'lblCurrentCOlorScheme
        '
        Me.lblCurrentCOlorScheme.AutoSize = True
        Me.lblCurrentCOlorScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblCurrentCOlorScheme.Location = New System.Drawing.Point(23, 58)
        Me.lblCurrentCOlorScheme.Name = "lblCurrentCOlorScheme"
        Me.lblCurrentCOlorScheme.Size = New System.Drawing.Size(108, 18)
        Me.lblCurrentCOlorScheme.TabIndex = 6
        Me.lblCurrentCOlorScheme.Text = "Color Scheme:"
        '
        'lblColorSchemeEditor
        '
        Me.lblColorSchemeEditor.AutoSize = True
        Me.lblColorSchemeEditor.Font = New System.Drawing.Font("Microsoft Sans Serif", 14.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblColorSchemeEditor.Location = New System.Drawing.Point(22, 12)
        Me.lblColorSchemeEditor.Name = "lblColorSchemeEditor"
        Me.lblColorSchemeEditor.Size = New System.Drawing.Size(204, 24)
        Me.lblColorSchemeEditor.TabIndex = 5
        Me.lblColorSchemeEditor.Text = "Color Scheme Editor"
        '
        'btnColor9
        '
        Me.btnColor9.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnColor9.Location = New System.Drawing.Point(1054, 514)
        Me.btnColor9.Name = "btnColor9"
        Me.btnColor9.Size = New System.Drawing.Size(78, 34)
        Me.btnColor9.TabIndex = 4
        Me.btnColor9.Text = "Color 9 ->"
        Me.btnColor9.UseVisualStyleBackColor = True
        '
        'btnColor8
        '
        Me.btnColor8.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnColor8.Location = New System.Drawing.Point(1055, 465)
        Me.btnColor8.Name = "btnColor8"
        Me.btnColor8.Size = New System.Drawing.Size(78, 34)
        Me.btnColor8.TabIndex = 4
        Me.btnColor8.Text = "Color 8 ->"
        Me.btnColor8.UseVisualStyleBackColor = True
        '
        'btnColor7
        '
        Me.btnColor7.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnColor7.Location = New System.Drawing.Point(1055, 416)
        Me.btnColor7.Name = "btnColor7"
        Me.btnColor7.Size = New System.Drawing.Size(78, 34)
        Me.btnColor7.TabIndex = 4
        Me.btnColor7.Text = "Color 7 ->"
        Me.btnColor7.UseVisualStyleBackColor = True
        '
        'btnColor6
        '
        Me.btnColor6.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnColor6.Location = New System.Drawing.Point(1055, 368)
        Me.btnColor6.Name = "btnColor6"
        Me.btnColor6.Size = New System.Drawing.Size(78, 34)
        Me.btnColor6.TabIndex = 4
        Me.btnColor6.Text = "Color 6 ->"
        Me.btnColor6.UseVisualStyleBackColor = True
        '
        'btnColor5
        '
        Me.btnColor5.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnColor5.Location = New System.Drawing.Point(1055, 322)
        Me.btnColor5.Name = "btnColor5"
        Me.btnColor5.Size = New System.Drawing.Size(78, 34)
        Me.btnColor5.TabIndex = 4
        Me.btnColor5.Text = "Color 5 ->"
        Me.btnColor5.UseVisualStyleBackColor = True
        '
        'btnColor4
        '
        Me.btnColor4.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnColor4.Location = New System.Drawing.Point(1054, 273)
        Me.btnColor4.Name = "btnColor4"
        Me.btnColor4.Size = New System.Drawing.Size(78, 34)
        Me.btnColor4.TabIndex = 4
        Me.btnColor4.Text = "Color 4 ->"
        Me.btnColor4.UseVisualStyleBackColor = True
        '
        'btnColor3
        '
        Me.btnColor3.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnColor3.Location = New System.Drawing.Point(1054, 225)
        Me.btnColor3.Name = "btnColor3"
        Me.btnColor3.Size = New System.Drawing.Size(78, 34)
        Me.btnColor3.TabIndex = 4
        Me.btnColor3.Text = "Color 3 ->"
        Me.btnColor3.UseVisualStyleBackColor = True
        '
        'btnCOlor2
        '
        Me.btnCOlor2.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnCOlor2.Location = New System.Drawing.Point(1055, 176)
        Me.btnCOlor2.Name = "btnCOlor2"
        Me.btnCOlor2.Size = New System.Drawing.Size(78, 34)
        Me.btnCOlor2.TabIndex = 4
        Me.btnCOlor2.Text = "Color 2 ->"
        Me.btnCOlor2.UseVisualStyleBackColor = True
        '
        'btnColor1
        '
        Me.btnColor1.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnColor1.Location = New System.Drawing.Point(1055, 128)
        Me.btnColor1.Name = "btnColor1"
        Me.btnColor1.Size = New System.Drawing.Size(78, 34)
        Me.btnColor1.TabIndex = 4
        Me.btnColor1.Text = "Color 1 ->"
        Me.btnColor1.UseVisualStyleBackColor = True
        '
        'lblColorPicker
        '
        Me.lblColorPicker.AutoSize = True
        Me.lblColorPicker.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblColorPicker.Location = New System.Drawing.Point(596, 101)
        Me.lblColorPicker.Name = "lblColorPicker"
        Me.lblColorPicker.Size = New System.Drawing.Size(93, 20)
        Me.lblColorPicker.TabIndex = 2
        Me.lblColorPicker.Text = "Color Picker"
        '
        'Panel2
        '
        Me.Panel2.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Panel2.Location = New System.Drawing.Point(598, 125)
        Me.Panel2.Name = "Panel2"
        Me.Panel2.Size = New System.Drawing.Size(445, 425)
        Me.Panel2.TabIndex = 1
        '
        'pnlScheme
        '
        Me.pnlScheme.Location = New System.Drawing.Point(1139, 139)
        Me.pnlScheme.Name = "pnlScheme"
        Me.pnlScheme.Size = New System.Drawing.Size(55, 399)
        Me.pnlScheme.TabIndex = 1
        '
        'btnDeleteColorScheme
        '
        Me.btnDeleteColorScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnDeleteColorScheme.Location = New System.Drawing.Point(470, 528)
        Me.btnDeleteColorScheme.Name = "btnDeleteColorScheme"
        Me.btnDeleteColorScheme.Size = New System.Drawing.Size(89, 28)
        Me.btnDeleteColorScheme.TabIndex = 0
        Me.btnDeleteColorScheme.Text = "Delete"
        Me.btnDeleteColorScheme.UseVisualStyleBackColor = True
        '
        'btnRenameColorScheme
        '
        Me.btnRenameColorScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnRenameColorScheme.Location = New System.Drawing.Point(376, 528)
        Me.btnRenameColorScheme.Name = "btnRenameColorScheme"
        Me.btnRenameColorScheme.Size = New System.Drawing.Size(88, 28)
        Me.btnRenameColorScheme.TabIndex = 0
        Me.btnRenameColorScheme.Text = "Rename"
        Me.btnRenameColorScheme.UseVisualStyleBackColor = True
        '
        'btnSaveAsColorScheme
        '
        Me.btnSaveAsColorScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnSaveAsColorScheme.Location = New System.Drawing.Point(215, 528)
        Me.btnSaveAsColorScheme.Name = "btnSaveAsColorScheme"
        Me.btnSaveAsColorScheme.Size = New System.Drawing.Size(88, 28)
        Me.btnSaveAsColorScheme.TabIndex = 0
        Me.btnSaveAsColorScheme.Text = "Save As"
        Me.btnSaveAsColorScheme.UseVisualStyleBackColor = True
        '
        'btnSaveCurrentScheme
        '
        Me.btnSaveCurrentScheme.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnSaveCurrentScheme.Location = New System.Drawing.Point(121, 528)
        Me.btnSaveCurrentScheme.Name = "btnSaveCurrentScheme"
        Me.btnSaveCurrentScheme.Size = New System.Drawing.Size(88, 28)
        Me.btnSaveCurrentScheme.TabIndex = 0
        Me.btnSaveCurrentScheme.Text = "Save "
        Me.btnSaveCurrentScheme.UseVisualStyleBackColor = True
        '
        'btnSaveConfiguration
        '
        Me.btnSaveConfiguration.BackColor = System.Drawing.Color.Azure
        Me.btnSaveConfiguration.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnSaveConfiguration.Location = New System.Drawing.Point(1503, 840)
        Me.btnSaveConfiguration.Name = "btnSaveConfiguration"
        Me.btnSaveConfiguration.Size = New System.Drawing.Size(259, 53)
        Me.btnSaveConfiguration.TabIndex = 2
        Me.btnSaveConfiguration.Text = "Save changes to project"
        Me.btnSaveConfiguration.UseVisualStyleBackColor = False
        '
        'btnStartSimulation
        '
        Me.btnStartSimulation.BackColor = System.Drawing.Color.Azure
        Me.btnStartSimulation.Font = New System.Drawing.Font("Microsoft Sans Serif", 15.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnStartSimulation.Location = New System.Drawing.Point(18, 23)
        Me.btnStartSimulation.Name = "btnStartSimulation"
        Me.btnStartSimulation.Size = New System.Drawing.Size(216, 42)
        Me.btnStartSimulation.TabIndex = 7
        Me.btnStartSimulation.Text = "Start Simulation"
        Me.btnStartSimulation.UseVisualStyleBackColor = False
        '
        'btnPauzeResume
        '
        Me.btnPauzeResume.BackColor = System.Drawing.Color.Azure
        Me.btnPauzeResume.Font = New System.Drawing.Font("Microsoft Sans Serif", 15.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnPauzeResume.Location = New System.Drawing.Point(18, 71)
        Me.btnPauzeResume.Name = "btnPauzeResume"
        Me.btnPauzeResume.Size = New System.Drawing.Size(215, 46)
        Me.btnPauzeResume.TabIndex = 2
        Me.btnPauzeResume.Text = "Pauze simulation"
        Me.btnPauzeResume.UseVisualStyleBackColor = False
        '
        'MenuStrip1
        '
        Me.MenuStrip1.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.itemFile, Me.InfoToolStripMenuItem})
        Me.MenuStrip1.Location = New System.Drawing.Point(0, 0)
        Me.MenuStrip1.Name = "MenuStrip1"
        Me.MenuStrip1.Size = New System.Drawing.Size(1781, 28)
        Me.MenuStrip1.TabIndex = 1
        Me.MenuStrip1.Text = "MenuStrip1"
        '
        'itemFile
        '
        Me.itemFile.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.NewToolStripMenuItem, Me.OpenToolStripMenuItem, Me.ChooseProjectRootToolStripMenuItem, Me.SetCValueToolStripMenuItem, Me.SaveToolStripMenuItem, Me.ExitToolStripMenuItem})
        Me.itemFile.Font = New System.Drawing.Font("Segoe UI", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.itemFile.Name = "itemFile"
        Me.itemFile.Size = New System.Drawing.Size(44, 24)
        Me.itemFile.Text = "File"
        '
        'NewToolStripMenuItem
        '
        Me.NewToolStripMenuItem.Name = "NewToolStripMenuItem"
        Me.NewToolStripMenuItem.Size = New System.Drawing.Size(210, 24)
        Me.NewToolStripMenuItem.Text = "New"
        '
        'OpenToolStripMenuItem
        '
        Me.OpenToolStripMenuItem.Name = "OpenToolStripMenuItem"
        Me.OpenToolStripMenuItem.Size = New System.Drawing.Size(210, 24)
        Me.OpenToolStripMenuItem.Text = "Open"
        '
        'ChooseProjectRootToolStripMenuItem
        '
        Me.ChooseProjectRootToolStripMenuItem.Name = "ChooseProjectRootToolStripMenuItem"
        Me.ChooseProjectRootToolStripMenuItem.Size = New System.Drawing.Size(210, 24)
        Me.ChooseProjectRootToolStripMenuItem.Text = "Choose project root"
        '
        'SetCValueToolStripMenuItem
        '
        Me.SetCValueToolStripMenuItem.Name = "SetCValueToolStripMenuItem"
        Me.SetCValueToolStripMenuItem.Size = New System.Drawing.Size(210, 24)
        Me.SetCValueToolStripMenuItem.Text = "Set c-value"
        '
        'SaveToolStripMenuItem
        '
        Me.SaveToolStripMenuItem.Name = "SaveToolStripMenuItem"
        Me.SaveToolStripMenuItem.Size = New System.Drawing.Size(210, 24)
        Me.SaveToolStripMenuItem.Text = "Save"
        '
        'ExitToolStripMenuItem
        '
        Me.ExitToolStripMenuItem.Name = "ExitToolStripMenuItem"
        Me.ExitToolStripMenuItem.Size = New System.Drawing.Size(210, 24)
        Me.ExitToolStripMenuItem.Text = "Exit"
        '
        'InfoToolStripMenuItem
        '
        Me.InfoToolStripMenuItem.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.HelpToolStripMenuItem, Me.AboutToolStripMenuItem})
        Me.InfoToolStripMenuItem.Font = New System.Drawing.Font("Segoe UI", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.InfoToolStripMenuItem.Name = "InfoToolStripMenuItem"
        Me.InfoToolStripMenuItem.Size = New System.Drawing.Size(47, 24)
        Me.InfoToolStripMenuItem.Text = "Info"
        '
        'HelpToolStripMenuItem
        '
        Me.HelpToolStripMenuItem.Name = "HelpToolStripMenuItem"
        Me.HelpToolStripMenuItem.Size = New System.Drawing.Size(119, 24)
        Me.HelpToolStripMenuItem.Text = "Help"
        '
        'AboutToolStripMenuItem
        '
        Me.AboutToolStripMenuItem.Name = "AboutToolStripMenuItem"
        Me.AboutToolStripMenuItem.Size = New System.Drawing.Size(119, 24)
        Me.AboutToolStripMenuItem.Text = "About"
        '
        'lblProjectName
        '
        Me.lblProjectName.AutoSize = True
        Me.lblProjectName.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblProjectName.Location = New System.Drawing.Point(1101, 13)
        Me.lblProjectName.Name = "lblProjectName"
        Me.lblProjectName.Size = New System.Drawing.Size(96, 18)
        Me.lblProjectName.TabIndex = 2
        Me.lblProjectName.Text = "Project name"
        '
        'txtProjectName
        '
        Me.txtProjectName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtProjectName.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.txtProjectName.Location = New System.Drawing.Point(1203, 11)
        Me.txtProjectName.Name = "txtProjectName"
        Me.txtProjectName.ReadOnly = True
        Me.txtProjectName.Size = New System.Drawing.Size(292, 24)
        Me.txtProjectName.TabIndex = 3
        '
        'txtRootFolder
        '
        Me.txtRootFolder.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtRootFolder.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.txtRootFolder.Location = New System.Drawing.Point(145, 11)
        Me.txtRootFolder.Name = "txtRootFolder"
        Me.txtRootFolder.ReadOnly = True
        Me.txtRootFolder.Size = New System.Drawing.Size(810, 24)
        Me.txtRootFolder.TabIndex = 3
        '
        'lblRootFolder
        '
        Me.lblRootFolder.AutoSize = True
        Me.lblRootFolder.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblRootFolder.Location = New System.Drawing.Point(12, 13)
        Me.lblRootFolder.Name = "lblRootFolder"
        Me.lblRootFolder.Size = New System.Drawing.Size(127, 18)
        Me.lblRootFolder.TabIndex = 2
        Me.lblRootFolder.Text = "Project root folder"
        '
        'btnBrowseRoot
        '
        Me.btnBrowseRoot.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnBrowseRoot.Location = New System.Drawing.Point(966, 8)
        Me.btnBrowseRoot.Name = "btnBrowseRoot"
        Me.btnBrowseRoot.Size = New System.Drawing.Size(124, 29)
        Me.btnBrowseRoot.TabIndex = 4
        Me.btnBrowseRoot.TabStop = False
        Me.btnBrowseRoot.Text = "Browse root"
        Me.btnBrowseRoot.UseVisualStyleBackColor = True
        '
        'StatusStrip1
        '
        Me.StatusStrip1.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripStatusLabel1})
        Me.StatusStrip1.Location = New System.Drawing.Point(0, 967)
        Me.StatusStrip1.Name = "StatusStrip1"
        Me.StatusStrip1.Size = New System.Drawing.Size(1781, 22)
        Me.StatusStrip1.TabIndex = 5
        Me.StatusStrip1.Text = "StatusStrip1"
        '
        'ToolStripStatusLabel1
        '
        Me.ToolStripStatusLabel1.Name = "ToolStripStatusLabel1"
        Me.ToolStripStatusLabel1.Size = New System.Drawing.Size(120, 17)
        Me.ToolStripStatusLabel1.Text = "ToolStripStatusLabel1"
        '
        'grpNavigateTabs
        '
        Me.grpNavigateTabs.Controls.Add(Me.btnGotoPreview)
        Me.grpNavigateTabs.Controls.Add(Me.btnGotoProbes)
        Me.grpNavigateTabs.Controls.Add(Me.btnGotoSources)
        Me.grpNavigateTabs.Controls.Add(Me.btnGotoConfiguration)
        Me.grpNavigateTabs.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpNavigateTabs.Location = New System.Drawing.Point(511, 827)
        Me.grpNavigateTabs.Name = "grpNavigateTabs"
        Me.grpNavigateTabs.Size = New System.Drawing.Size(858, 76)
        Me.grpNavigateTabs.TabIndex = 8
        Me.grpNavigateTabs.TabStop = False
        Me.grpNavigateTabs.Text = "Quick navigate to"
        '
        'btnGotoPreview
        '
        Me.btnGotoPreview.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnGotoPreview.Location = New System.Drawing.Point(635, 27)
        Me.btnGotoPreview.Name = "btnGotoPreview"
        Me.btnGotoPreview.Size = New System.Drawing.Size(194, 31)
        Me.btnGotoPreview.TabIndex = 0
        Me.btnGotoPreview.Text = "Preview"
        Me.btnGotoPreview.UseVisualStyleBackColor = True
        '
        'btnGotoProbes
        '
        Me.btnGotoProbes.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnGotoProbes.Location = New System.Drawing.Point(431, 27)
        Me.btnGotoProbes.Name = "btnGotoProbes"
        Me.btnGotoProbes.Size = New System.Drawing.Size(198, 31)
        Me.btnGotoProbes.TabIndex = 0
        Me.btnGotoProbes.Text = "Probes"
        Me.btnGotoProbes.UseVisualStyleBackColor = True
        '
        'btnGotoSources
        '
        Me.btnGotoSources.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnGotoSources.Location = New System.Drawing.Point(228, 27)
        Me.btnGotoSources.Name = "btnGotoSources"
        Me.btnGotoSources.Size = New System.Drawing.Size(197, 31)
        Me.btnGotoSources.TabIndex = 0
        Me.btnGotoSources.Text = "Sources"
        Me.btnGotoSources.UseVisualStyleBackColor = True
        '
        'btnGotoConfiguration
        '
        Me.btnGotoConfiguration.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnGotoConfiguration.Location = New System.Drawing.Point(25, 27)
        Me.btnGotoConfiguration.Name = "btnGotoConfiguration"
        Me.btnGotoConfiguration.Size = New System.Drawing.Size(197, 31)
        Me.btnGotoConfiguration.TabIndex = 0
        Me.btnGotoConfiguration.Text = "Settings"
        Me.btnGotoConfiguration.UseVisualStyleBackColor = True
        '
        'grpSimulationControlButtons
        '
        Me.grpSimulationControlButtons.BackColor = System.Drawing.SystemColors.Control
        Me.grpSimulationControlButtons.Controls.Add(Me.btnPauzeResume)
        Me.grpSimulationControlButtons.Controls.Add(Me.btnStartSimulation)
        Me.grpSimulationControlButtons.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpSimulationControlButtons.Location = New System.Drawing.Point(16, 827)
        Me.grpSimulationControlButtons.Name = "grpSimulationControlButtons"
        Me.grpSimulationControlButtons.Size = New System.Drawing.Size(262, 127)
        Me.grpSimulationControlButtons.TabIndex = 9
        Me.grpSimulationControlButtons.TabStop = False
        Me.grpSimulationControlButtons.Text = "Simulation Control"
        '
        'Panel1
        '
        Me.Panel1.BackColor = System.Drawing.Color.Azure
        Me.Panel1.Controls.Add(Me.btnDescription)
        Me.Panel1.Controls.Add(Me.txtRootFolder)
        Me.Panel1.Controls.Add(Me.txtProjectName)
        Me.Panel1.Controls.Add(Me.lblRootFolder)
        Me.Panel1.Controls.Add(Me.lblProjectName)
        Me.Panel1.Controls.Add(Me.btnBrowseRoot)
        Me.Panel1.Location = New System.Drawing.Point(19, 33)
        Me.Panel1.Name = "Panel1"
        Me.Panel1.Size = New System.Drawing.Size(1734, 43)
        Me.Panel1.TabIndex = 10
        '
        'btnDescription
        '
        Me.btnDescription.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnDescription.Location = New System.Drawing.Point(1536, 8)
        Me.btnDescription.Name = "btnDescription"
        Me.btnDescription.Size = New System.Drawing.Size(168, 29)
        Me.btnDescription.TabIndex = 5
        Me.btnDescription.Text = "Edit description"
        Me.btnDescription.UseVisualStyleBackColor = True
        '
        'grpShareImage
        '
        Me.grpShareImage.Controls.Add(Me.btnShareIntensityImage)
        Me.grpShareImage.Controls.Add(Me.btnShareFieldImage)
        Me.grpShareImage.Controls.Add(Me.btnShareSceneImage)
        Me.grpShareImage.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.grpShareImage.Location = New System.Drawing.Point(295, 827)
        Me.grpShareImage.Name = "grpShareImage"
        Me.grpShareImage.Size = New System.Drawing.Size(199, 125)
        Me.grpShareImage.TabIndex = 11
        Me.grpShareImage.TabStop = False
        Me.grpShareImage.Text = "Share with source / probe"
        '
        'btnShareIntensityImage
        '
        Me.btnShareIntensityImage.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnShareIntensityImage.Location = New System.Drawing.Point(14, 86)
        Me.btnShareIntensityImage.Name = "btnShareIntensityImage"
        Me.btnShareIntensityImage.Size = New System.Drawing.Size(154, 27)
        Me.btnShareIntensityImage.TabIndex = 0
        Me.btnShareIntensityImage.Text = "Intensity image"
        Me.btnShareIntensityImage.UseVisualStyleBackColor = True
        '
        'btnShareFieldImage
        '
        Me.btnShareFieldImage.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnShareFieldImage.Location = New System.Drawing.Point(16, 53)
        Me.btnShareFieldImage.Name = "btnShareFieldImage"
        Me.btnShareFieldImage.Size = New System.Drawing.Size(154, 27)
        Me.btnShareFieldImage.TabIndex = 0
        Me.btnShareFieldImage.Text = "Field image"
        Me.btnShareFieldImage.UseVisualStyleBackColor = True
        '
        'btnShareSceneImage
        '
        Me.btnShareSceneImage.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnShareSceneImage.Location = New System.Drawing.Point(15, 21)
        Me.btnShareSceneImage.Name = "btnShareSceneImage"
        Me.btnShareSceneImage.Size = New System.Drawing.Size(155, 27)
        Me.btnShareSceneImage.TabIndex = 0
        Me.btnShareSceneImage.Text = "Scene image"
        Me.btnShareSceneImage.UseVisualStyleBackColor = True
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1781, 989)
        Me.Controls.Add(Me.grpShareImage)
        Me.Controls.Add(Me.Panel1)
        Me.Controls.Add(Me.grpSimulationControlButtons)
        Me.Controls.Add(Me.grpNavigateTabs)
        Me.Controls.Add(Me.StatusStrip1)
        Me.Controls.Add(Me.tabMainFunctions)
        Me.Controls.Add(Me.MenuStrip1)
        Me.Controls.Add(Me.btnSaveConfiguration)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.MainMenuStrip = Me.MenuStrip1
        Me.Name = "Form1"
        Me.Text = "Wave Dude 2D   V0.29"
        Me.tabMainFunctions.ResumeLayout(False)
        Me.tabConfiguration.ResumeLayout(False)
        Me.grpProjects.ResumeLayout(False)
        Me.grpSimulationSettings.ResumeLayout(False)
        Me.grpSimulationSettings.PerformLayout()
        CType(Me.numTimeDecayFactor, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numSimulationSteps, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numQuality, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numIntensityScaling, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numOffsetIntensity, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numOffsetField, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numFieldScaling, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numVisualizeEvery, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpSceneProperties.ResumeLayout(False)
        Me.grpSceneProperties.PerformLayout()
        CType(Me.numRefractiveIndex, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.picImagePreview, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numScaleImageSource, System.ComponentModel.ISupportInitialize).EndInit()
        Me.tabSourceList.ResumeLayout(False)
        Me.tabSourceList.PerformLayout()
        CType(Me.picPreviewSource, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpPlaceSource.ResumeLayout(False)
        Me.pnlPosition.ResumeLayout(False)
        Me.pnlPosition.PerformLayout()
        CType(Me.numSourcePositionX, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numSourcePositionY, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpSourceDetails.ResumeLayout(False)
        Me.grpSourceDetails.PerformLayout()
        Me.grpComposite.ResumeLayout(False)
        Me.grpComposite.PerformLayout()
        CType(Me.numRandomizeOffWidth, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numRandomizePosition, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.NumRandomizePhase, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numRandomizeFrequency, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numSpacingSizeY, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numSpacingSizeX, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numArraySizeY, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numArraySizeX, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpSingleSource.ResumeLayout(False)
        Me.grpSingleSource.PerformLayout()
        CType(Me.numOffStateCycles, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numPhase, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numAmplitude, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numFrequency, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numPulseWidthCycles, System.ComponentModel.ISupportInitialize).EndInit()
        Me.tabProbeList.ResumeLayout(False)
        Me.GroupBox2.ResumeLayout(False)
        Me.GroupBox2.PerformLayout()
        Me.grpProbe2.ResumeLayout(False)
        Me.grpProbe2.PerformLayout()
        CType(Me.numRunningWindow2, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numBPosY2, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numAPosY2, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numBPosX2, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numAPosX2, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpProbe1.ResumeLayout(False)
        Me.grpProbe1.PerformLayout()
        CType(Me.numRunningWindow, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numBPosY, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numAPosY, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numBPosX, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numAPosX, System.ComponentModel.ISupportInitialize).EndInit()
        Me.tabViewer.ResumeLayout(False)
        Me.tabViewer.PerformLayout()
        Me.pnlPreviewIntensityProps.ResumeLayout(False)
        Me.pnlPreviewIntensityProps.PerformLayout()
        CType(Me.numBrightnessIntensityOverlay, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numIntensityScaling2, System.ComponentModel.ISupportInitialize).EndInit()
        Me.Panel3.ResumeLayout(False)
        Me.Panel3.PerformLayout()
        CType(Me.numFieldScaling2, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numBrightnessFieldOverlay, System.ComponentModel.ISupportInitialize).EndInit()
        Me.tabOutput.ResumeLayout(False)
        Me.tabOutput.PerformLayout()
        Me.tabColorScheme.ResumeLayout(False)
        Me.tabColorScheme.PerformLayout()
        Me.MenuStrip1.ResumeLayout(False)
        Me.MenuStrip1.PerformLayout()
        Me.StatusStrip1.ResumeLayout(False)
        Me.StatusStrip1.PerformLayout()
        Me.grpNavigateTabs.ResumeLayout(False)
        Me.grpSimulationControlButtons.ResumeLayout(False)
        Me.Panel1.ResumeLayout(False)
        Me.Panel1.PerformLayout()
        Me.grpShareImage.ResumeLayout(False)
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents tabMainFunctions As TabControl
    Friend WithEvents tabColorScheme As TabPage
    Friend WithEvents txtCurrentColorScheme As TextBox
    Friend WithEvents lblCurrentCOlorScheme As Label
    Friend WithEvents lblColorSchemeEditor As Label
    Friend WithEvents btnColor5 As Button
    Friend WithEvents btnColor4 As Button
    Friend WithEvents btnColor3 As Button
    Friend WithEvents btnCOlor2 As Button
    Friend WithEvents btnColor1 As Button
    Friend WithEvents lblColorPicker As Label
    Friend WithEvents Panel2 As Panel
    Friend WithEvents pnlScheme As Panel
    Friend WithEvents btnSaveAsColorScheme As Button
    Friend WithEvents btnSaveCurrentScheme As Button
    Friend WithEvents tabConfiguration As TabPage
    Friend WithEvents tabProbeList As TabPage
    Friend WithEvents tabOutput As TabPage
    Friend WithEvents tabViewer As TabPage
    Friend WithEvents ListView1 As ListView
    Friend WithEvents MenuStrip1 As MenuStrip
    Friend WithEvents itemFile As ToolStripMenuItem
    Friend WithEvents NewToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents OpenToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents SaveToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents ExitToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents lblSchemeFolder As Label
    Friend WithEvents btnLoadScheme As Button
    Friend WithEvents lblType As Label
    Friend WithEvents radSchemeTypeIntensity As RadioButton
    Friend WithEvents radSchemeTypeField As RadioButton
    Friend WithEvents btbChooseFFolder As Button
    Friend WithEvents txtSchemeFolder As TextBox
    Friend WithEvents btnColor7 As Button
    Friend WithEvents btnColor6 As Button
    Friend WithEvents lblProjectName As Label
    Friend WithEvents txtProjectName As TextBox
    Friend WithEvents txtRootFolder As TextBox
    Friend WithEvents lblRootFolder As Label
    Friend WithEvents btnBrowseRoot As Button
    Friend WithEvents btnSaveAs As Button
    Friend WithEvents grpSceneProperties As GroupBox
    Friend WithEvents txtDampingEdgeSize As TextBox
    Friend WithEvents chkUseImageSources As CheckBox
    Friend WithEvents chkDampingEdge As CheckBox
    Friend WithEvents btnLoadSceneImage As Button
    Friend WithEvents lblImageNameAndSize As Label
    Friend WithEvents Label1 As Label
    Friend WithEvents lblPix As Label
    Friend WithEvents lblX As Label
    Friend WithEvents txtYsize As TextBox
    Friend WithEvents txtXSize As TextBox
    Friend WithEvents radUserDefinedScene As RadioButton
    Friend WithEvents picImagePreview As PictureBox
    Friend WithEvents radUseSceneImage As RadioButton
    Friend WithEvents tabSourceList As TabPage
    Friend WithEvents btnSaveConfiguration As Button
    Friend WithEvents grpSimulationSettings As GroupBox
    Friend WithEvents numVisualizeEvery As NumericUpDown
    Friend WithEvents Label4 As Label
    Friend WithEvents lblVisualizeEvery As Label
    Friend WithEvents lblSimulationSteps As Label
    Friend WithEvents chkPreviewVideoIntensity As CheckBox
    Friend WithEvents chkExportVideoIntensity As CheckBox
    Friend WithEvents chkPreviewVideoField As CheckBox
    Friend WithEvents chkExportVideoField As CheckBox
    Friend WithEvents lblPreviewVideo As Label
    Friend WithEvents lblExportVideo As Label
    Friend WithEvents pnlPreviewIntensity As Panel
    Friend WithEvents pnlPreviewField As Panel
    Friend WithEvents cmbFieldColorScheme As ComboBox
    Friend WithEvents Label3 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents cmbIntensityColorScheme As ComboBox
    Friend WithEvents btnStartSimulation As Button
    Friend WithEvents numFieldScaling As NumericUpDown
    Friend WithEvents Label5 As Label
    Friend WithEvents numIntensityScaling As NumericUpDown
    Friend WithEvents numScaleIntensityValues As Label
    Friend WithEvents btnEditObjectSourceList As Button
    Friend WithEvents chkUseObjectSources As CheckBox
    Friend WithEvents lblScaleImgSourceIntensity As Label
    Friend WithEvents numScaleImageSource As NumericUpDown
    Friend WithEvents numOffsetIntensity As NumericUpDown
    Friend WithEvents numOffsetField As NumericUpDown
    Friend WithEvents Label6 As Label
    Friend WithEvents lblOffsetField As Label
    Friend WithEvents btnViewIntensityVideo As Button
    Friend WithEvents btnViewFieldVideo As Button
    Friend WithEvents lblVideoQuality As Label
    Friend WithEvents numQuality As NumericUpDown
    Friend WithEvents chkUseGPU As CheckBox
    Friend WithEvents Label7 As Label
    Friend WithEvents StatusStrip1 As StatusStrip
    Friend WithEvents ToolStripStatusLabel1 As ToolStripStatusLabel
    Friend WithEvents btnPauzeResume As Button
    Friend WithEvents pnlSourceImage As Panel
    Friend WithEvents pnlProbeImage As Panel
    Friend WithEvents grpProbe1 As GroupBox
    Friend WithEvents radFullScale As RadioButton
    Friend WithEvents radRunningScale As RadioButton
    Friend WithEvents chkAutoScaleY As CheckBox
    Friend WithEvents chkAutoScaleX As CheckBox
    Friend WithEvents numBPosY As NumericUpDown
    Friend WithEvents numAPosY As NumericUpDown
    Friend WithEvents numBPosX As NumericUpDown
    Friend WithEvents numAPosX As NumericUpDown
    Friend WithEvents butPicPositions As Button
    Friend WithEvents Label16 As Label
    Friend WithEvents cmbPlaceOn As ComboBox
    Friend WithEvents cmbMeasurement As ComboBox
    Friend WithEvents Label13 As Label
    Friend WithEvents Label15 As Label
    Friend WithEvents cmbBoxSize As ComboBox
    Friend WithEvents cmbProbeType As ComboBox
    Friend WithEvents Label14 As Label
    Friend WithEvents Label12 As Label
    Friend WithEvents Label11 As Label
    Friend WithEvents Label17 As Label
    Friend WithEvents Label10 As Label
    Friend WithEvents Label9 As Label
    Friend WithEvents Label8 As Label
    Friend WithEvents txtStartAfterStep As TextBox
    Friend WithEvents chkStartAfterStep As CheckBox
    Friend WithEvents Label19 As Label
    Friend WithEvents Label18 As Label
    Friend WithEvents txtMaxScaleY As TextBox
    Friend WithEvents txtMinScaleY As TextBox
    Friend WithEvents Label20 As Label
    Friend WithEvents numRefractiveIndex As NumericUpDown
    Friend WithEvents chkProbeOn1 As CheckBox
    Friend WithEvents Label21 As Label
    Friend WithEvents numRunningWindow As NumericUpDown
    Friend WithEvents grpProbe2 As GroupBox
    Friend WithEvents Label22 As Label
    Friend WithEvents numRunningWindow2 As NumericUpDown
    Friend WithEvents chkProbeOn2 As CheckBox
    Friend WithEvents txtStartAfterStep2 As TextBox
    Friend WithEvents chkStartAfterStep2 As CheckBox
    Friend WithEvents Label23 As Label
    Friend WithEvents Label24 As Label
    Friend WithEvents txtMaxScaleY2 As TextBox
    Friend WithEvents txtMinScaleY2 As TextBox
    Friend WithEvents radFullScale2 As RadioButton
    Friend WithEvents radRunningScale2 As RadioButton
    Friend WithEvents chkAutoScaleY2 As CheckBox
    Friend WithEvents chkAutoScaleX2 As CheckBox
    Friend WithEvents numBPosY2 As NumericUpDown
    Friend WithEvents numAPosY2 As NumericUpDown
    Friend WithEvents numBPosX2 As NumericUpDown
    Friend WithEvents numAPosX2 As NumericUpDown
    Friend WithEvents butPicPositions2 As Button
    Friend WithEvents Label25 As Label
    Friend WithEvents cmbPlaceOn2 As ComboBox
    Friend WithEvents cmbMeasurement2 As ComboBox
    Friend WithEvents Label26 As Label
    Friend WithEvents Label27 As Label
    Friend WithEvents cmbBoxSize2 As ComboBox
    Friend WithEvents cmbProbeType2 As ComboBox
    Friend WithEvents Label28 As Label
    Friend WithEvents Label29 As Label
    Friend WithEvents Label30 As Label
    Friend WithEvents Label31 As Label
    Friend WithEvents Label32 As Label
    Friend WithEvents Label33 As Label
    Friend WithEvents Label34 As Label
    Friend WithEvents btnDelete As Button
    Friend WithEvents lstSources As ListBox
    Friend WithEvents grpSourceDetails As GroupBox
    Friend WithEvents lblPosition As Label
    Friend WithEvents numSourcePositionY As NumericUpDown
    Friend WithEvents numSourcePositionX As NumericUpDown
    Friend WithEvents btnPickSourcePosition As Button
    Friend WithEvents Label36 As Label
    Friend WithEvents Label35 As Label
    Friend WithEvents btnCopy As Button
    Friend WithEvents grpSingleSource As GroupBox
    Friend WithEvents Label43 As Label
    Friend WithEvents Label42 As Label
    Friend WithEvents Label41 As Label
    Friend WithEvents Label40 As Label
    Friend WithEvents radModePulsed As RadioButton
    Friend WithEvents radModeContinuous As RadioButton
    Friend WithEvents txtName As TextBox
    Friend WithEvents cmbMainType As ComboBox
    Friend WithEvents Label39 As Label
    Friend WithEvents lblName As Label
    Friend WithEvents Label38 As Label
    Friend WithEvents numOffStateCycles As NumericUpDown
    Friend WithEvents numAmplitude As NumericUpDown
    Friend WithEvents numFrequency As NumericUpDown
    Friend WithEvents numPulseWidthCycles As NumericUpDown
    Friend WithEvents Label44 As Label
    Friend WithEvents Label46 As Label
    Friend WithEvents Label45 As Label
    Friend WithEvents cmbPulseShape As ComboBox
    Friend WithEvents grpComposite As GroupBox
    Friend WithEvents numSpacingSizeX As NumericUpDown
    Friend WithEvents numArraySizeX As NumericUpDown
    Friend WithEvents numRandomizeFrequency As NumericUpDown
    Friend WithEvents Label50 As Label
    Friend WithEvents Label51 As Label
    Friend WithEvents Label52 As Label
    Friend WithEvents Label53 As Label
    Friend WithEvents cmbCompositeProperties As ComboBox
    Friend WithEvents numPhase As NumericUpDown
    Friend WithEvents Label54 As Label
    Friend WithEvents numSpacingSizeY As NumericUpDown
    Friend WithEvents numArraySizeY As NumericUpDown
    Friend WithEvents btnSaveSource As Button
    Friend WithEvents numRandomizeOffWidth As NumericUpDown
    Friend WithEvents numRandomizePosition As NumericUpDown
    Friend WithEvents NumRandomizePhase As NumericUpDown
    Friend WithEvents chkRandomizePhase As CheckBox
    Friend WithEvents chkRandomizePosition As CheckBox
    Friend WithEvents chkOffWidth As CheckBox
    Friend WithEvents chkRandomizeFrequency As CheckBox
    Friend WithEvents btnOpenVideoFolder As Button
    Friend WithEvents txtDetachAfterSteps As TextBox
    Friend WithEvents Label47 As Label
    Friend WithEvents chkDetachSource As CheckBox
    Friend WithEvents pnlPosition As Panel
    Friend WithEvents lblLambdaIs As Label
    Friend WithEvents chkSourceOn As CheckBox
    Friend WithEvents btnRenameProject As Button
    Friend WithEvents btnDeleteProject As Button
    Friend WithEvents btnLoadProject As Button
    Friend WithEvents ChooseProjectRootToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents SetCValueToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents btnExportIntensityFrame As Button
    Friend WithEvents btnExportFieldFrame As Button
    Friend WithEvents btnOpenImageFolder As Button
    Friend WithEvents btnNew As Button
    Friend WithEvents lblWarningIntensity As Label
    Friend WithEvents lblWarningField As Label
    Friend WithEvents grpNavigateTabs As GroupBox
    Friend WithEvents grpSimulationControlButtons As GroupBox
    Friend WithEvents btnGotoPreview As Button
    Friend WithEvents btnGotoProbes As Button
    Friend WithEvents btnGotoSources As Button
    Friend WithEvents btnGotoConfiguration As Button
    Friend WithEvents Panel1 As Panel
    Friend WithEvents grpShareImage As GroupBox
    Friend WithEvents btnShareIntensityImage As Button
    Friend WithEvents btnShareFieldImage As Button
    Friend WithEvents btnShareSceneImage As Button
    Friend WithEvents btnNewProject As Button
    Friend WithEvents btnColor9 As Button
    Friend WithEvents btnColor8 As Button
    Friend WithEvents grpProjects As GroupBox
    Friend WithEvents grpPlaceSource As GroupBox
    Friend WithEvents GroupBox2 As GroupBox
    Friend WithEvents Label49 As Label
    Friend WithEvents Label48 As Label
    Friend WithEvents Label37 As Label
    Friend WithEvents btnDeleteColorScheme As Button
    Friend WithEvents btnRenameColorScheme As Button
    Friend WithEvents Label56 As Label
    Friend WithEvents Label55 As Label
    Friend WithEvents lstAllProjects As ListView
    Friend WithEvents colProjectName As ColumnHeader
    Friend WithEvents colLastEdit As ColumnHeader
    Friend WithEvents lstImages As ListView
    Friend WithEvents colImageName As ColumnHeader
    Friend WithEvents colCreatedOn As ColumnHeader
    Friend WithEvents lstVideo As ListView
    Friend WithEvents colVideoName As ColumnHeader
    Friend WithEvents lstAllProjects3 As ListView
    Friend WithEvents colProjectName2 As ColumnHeader
    Friend WithEvents colLastEdited2 As ColumnHeader
    Friend WithEvents btnDeleteScene As Button
    Friend WithEvents btnLoadExternal As Button
    Friend WithEvents pnlPreview As Panel
    Friend WithEvents Label57 As Label
    Friend WithEvents lstScenes As ListView
    Friend WithEvents colSceneName As ColumnHeader
    Friend WithEvents colSceneCreated As ColumnHeader
    Friend WithEvents btnOpenImageFolder2 As Button
    Friend WithEvents btnViewVIdeoInPlayer2 As Button
    Friend WithEvents btnOpenVIdeoFolder2 As Button
    Friend WithEvents lblPreview As Label
    Friend WithEvents btnDeleteVideos As Button
    Friend WithEvents btnDeleteImages As Button
    Friend WithEvents btnCopyToClipboard As Button
    Protected Friend WithEvents colCreatedOn2 As ColumnHeader
    Friend WithEvents btnShareWithEditors As Button
    Friend WithEvents chkUse4thOrderLaplacian As CheckBox
    Friend WithEvents lblInfo As Label
    Friend WithEvents btnRenameVideo As Button
    Friend WithEvents picPreviewSource As PictureBox
    Friend WithEvents chkAutoRescaleSourceOn As CheckBox
    Friend WithEvents numSimulationSteps As NumericUpDown
    Friend WithEvents Label59 As Label
    Friend WithEvents Label58 As Label
    Friend WithEvents Label60 As Label
    Friend WithEvents Label61 As Label
    Friend WithEvents numTimeDecayFactor As NumericUpDown
    Friend WithEvents Label62 As Label
    Friend WithEvents Label63 As Label
    Friend WithEvents numFieldScaling2 As NumericUpDown
    Friend WithEvents chkDiplayOverlayInField As CheckBox
    Friend WithEvents Label64 As Label
    Friend WithEvents numIntensityScaling2 As NumericUpDown
    Friend WithEvents chkDiplayOverlayInIntensity As CheckBox
    Friend WithEvents numBrightnessIntensityOverlay As NumericUpDown
    Friend WithEvents numBrightnessFieldOverlay As NumericUpDown
    Friend WithEvents Label66 As Label
    Friend WithEvents Label65 As Label
    Friend WithEvents pnlPreviewIntensityProps As Panel
    Friend WithEvents chkExportVideoIntensity2 As CheckBox
    Friend WithEvents Panel3 As Panel
    Friend WithEvents chkExportVideoField2 As CheckBox
    Friend WithEvents InfoToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents HelpToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents AboutToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents btnSaveConfiguration2 As Button
    Friend WithEvents btnDescription As Button
    Friend WithEvents btnPickRoot As Button
End Class

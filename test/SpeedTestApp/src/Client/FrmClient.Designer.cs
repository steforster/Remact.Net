namespace Remact.SpeedTest.Client
{
  partial class FrmClient
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose (bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose ();
      }
      base.Dispose (disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent ()
    {
            this.components = new System.ComponentModel.Container();
            this.labelClient = new System.Windows.Forms.Label();
            this.labelService = new System.Windows.Forms.Label();
            this.textBoxService = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.checkBoxSpeedTest = new System.Windows.Forms.CheckBox();
            this.labelState = new System.Windows.Forms.Label();
            this.checkBoxService = new System.Windows.Forms.CheckBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxCatalogHost = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboBoxTransport = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelClient
            // 
            this.labelClient.AutoSize = true;
            this.labelClient.Location = new System.Drawing.Point(2, 6);
            this.labelClient.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelClient.Name = "labelClient";
            this.labelClient.Size = new System.Drawing.Size(33, 13);
            this.labelClient.TabIndex = 0;
            this.labelClient.Text = "Client";
            // 
            // labelService
            // 
            this.labelService.AutoSize = true;
            this.labelService.Location = new System.Drawing.Point(0, 2);
            this.labelService.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelService.Name = "labelService";
            this.labelService.Size = new System.Drawing.Size(43, 13);
            this.labelService.TabIndex = 3;
            this.labelService.Text = "Service";
            // 
            // textBoxService
            // 
            this.textBoxService.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxService.Location = new System.Drawing.Point(0, 57);
            this.textBoxService.Margin = new System.Windows.Forms.Padding(2);
            this.textBoxService.Multiline = true;
            this.textBoxService.Name = "textBoxService";
            this.textBoxService.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBoxService.Size = new System.Drawing.Size(481, 204);
            this.textBoxService.TabIndex = 0;
            this.textBoxService.TabStop = false;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(2, 75);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.checkBoxSpeedTest);
            this.splitContainer1.Panel2.Controls.Add(this.labelState);
            this.splitContainer1.Panel2.Controls.Add(this.labelService);
            this.splitContainer1.Panel2.Controls.Add(this.textBoxService);
            this.splitContainer1.Size = new System.Drawing.Size(481, 283);
            this.splitContainer1.SplitterDistance = 27;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 5;
            // 
            // checkBoxSpeedTest
            // 
            this.checkBoxSpeedTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBoxSpeedTest.AutoSize = true;
            this.checkBoxSpeedTest.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.checkBoxSpeedTest.Location = new System.Drawing.Point(397, 36);
            this.checkBoxSpeedTest.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxSpeedTest.Name = "checkBoxSpeedTest";
            this.checkBoxSpeedTest.Size = new System.Drawing.Size(81, 17);
            this.checkBoxSpeedTest.TabIndex = 4;
            this.checkBoxSpeedTest.Text = "Speed Test";
            this.checkBoxSpeedTest.UseVisualStyleBackColor = true;
            // 
            // labelState
            // 
            this.labelState.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelState.Location = new System.Drawing.Point(389, 19);
            this.labelState.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelState.Name = "labelState";
            this.labelState.Size = new System.Drawing.Size(92, 15);
            this.labelState.TabIndex = 0;
            this.labelState.Text = "disconnected";
            this.labelState.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // checkBoxService
            // 
            this.checkBoxService.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBoxService.AutoSize = true;
            this.checkBoxService.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.checkBoxService.Location = new System.Drawing.Point(415, 57);
            this.checkBoxService.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxService.Name = "checkBoxService";
            this.checkBoxService.Size = new System.Drawing.Size(65, 17);
            this.checkBoxService.TabIndex = 3;
            this.checkBoxService.Text = "connect";
            this.checkBoxService.UseVisualStyleBackColor = true;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.Timer1_Tick);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(2, 57);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(66, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Catalog host";
            // 
            // textBoxCatalogHost
            // 
            this.textBoxCatalogHost.Location = new System.Drawing.Point(72, 54);
            this.textBoxCatalogHost.Margin = new System.Windows.Forms.Padding(2);
            this.textBoxCatalogHost.Name = "textBoxCatalogHost";
            this.textBoxCatalogHost.Size = new System.Drawing.Size(86, 20);
            this.textBoxCatalogHost.TabIndex = 1;
            this.textBoxCatalogHost.Text = "localhost";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(200, 57);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(52, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "Transport";
            // 
            // comboBoxTransport
            // 
            this.comboBoxTransport.FormattingEnabled = true;
            this.comboBoxTransport.IntegralHeight = false;
            this.comboBoxTransport.Items.AddRange(new object[] {
            "BMS1, TCP",
            "JSON, WebSocket"});
            this.comboBoxTransport.Location = new System.Drawing.Point(264, 53);
            this.comboBoxTransport.Name = "comboBoxTransport";
            this.comboBoxTransport.Size = new System.Drawing.Size(120, 21);
            this.comboBoxTransport.TabIndex = 1;
            this.comboBoxTransport.Tag = "";
            this.comboBoxTransport.SelectedIndexChanged += new System.EventHandler(this.comboBoxTransport_SelectedIndexChanged);
            // 
            // FrmClient
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 361);
            this.Controls.Add(this.comboBoxTransport);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxCatalogHost);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.checkBoxService);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.labelClient);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "FrmClient";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmClient_FormClosing);
            this.Shown += new System.EventHandler(this.FrmClient_FirstShown);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.Label labelClient;
    private System.Windows.Forms.Label labelService;
    private System.Windows.Forms.TextBox textBoxService;
    private System.Windows.Forms.SplitContainer splitContainer1;
    private System.Windows.Forms.CheckBox checkBoxService;
    private System.Windows.Forms.Timer timer1;
    private System.Windows.Forms.Label labelState;
    private System.Windows.Forms.CheckBox checkBoxSpeedTest;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.TextBox textBoxCatalogHost;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboBoxTransport;
    }
}


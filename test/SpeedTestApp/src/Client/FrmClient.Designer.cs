namespace Test2.Client
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
            this.ccheckBoxSpeedTest = new System.Windows.Forms.CheckBox();
            this.labelState = new System.Windows.Forms.Label();
            this.checkBoxService = new System.Windows.Forms.CheckBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxCatalogHost = new System.Windows.Forms.TextBox();
            //((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lbClient
            // 
            this.labelClient.AutoSize = true;
            this.labelClient.Location = new System.Drawing.Point(2, 6);
            this.labelClient.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelClient.Name = "lbClient";
            this.labelClient.Size = new System.Drawing.Size(33, 13);
            this.labelClient.TabIndex = 0;
            this.labelClient.Text = "Client";
            // 
            // lbService1
            // 
            this.labelService.AutoSize = true;
            this.labelService.Location = new System.Drawing.Point(0, 2);
            this.labelService.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelService.Name = "lbService1";
            this.labelService.Size = new System.Drawing.Size(43, 13);
            this.labelService.TabIndex = 3;
            this.labelService.Text = "Service";
            // 
            // tbService1
            // 
            this.textBoxService.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxService.Location = new System.Drawing.Point(0, 57);
            this.textBoxService.Margin = new System.Windows.Forms.Padding(2);
            this.textBoxService.Multiline = true;
            this.textBoxService.Name = "tbService1";
            this.textBoxService.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBoxService.Size = new System.Drawing.Size(481, 202);
            this.textBoxService.TabIndex = 4;
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
            this.splitContainer1.Panel2.Controls.Add(this.ccheckBoxSpeedTest);
            this.splitContainer1.Panel2.Controls.Add(this.labelState);
            this.splitContainer1.Panel2.Controls.Add(this.labelService);
            this.splitContainer1.Panel2.Controls.Add(this.textBoxService);
            this.splitContainer1.Size = new System.Drawing.Size(481, 283);
            this.splitContainer1.SplitterDistance = 27;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 5;
            // 
            // cbSpeedTest1
            // 
            this.ccheckBoxSpeedTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ccheckBoxSpeedTest.AutoSize = true;
            this.ccheckBoxSpeedTest.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ccheckBoxSpeedTest.Location = new System.Drawing.Point(397, 36);
            this.ccheckBoxSpeedTest.Margin = new System.Windows.Forms.Padding(2);
            this.ccheckBoxSpeedTest.Name = "cbSpeedTest1";
            this.ccheckBoxSpeedTest.Size = new System.Drawing.Size(81, 17);
            this.ccheckBoxSpeedTest.TabIndex = 6;
            this.ccheckBoxSpeedTest.Text = "Speed Test";
            this.ccheckBoxSpeedTest.UseVisualStyleBackColor = true;
            // 
            // lbState1
            // 
            this.labelState.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelState.Location = new System.Drawing.Point(389, 19);
            this.labelState.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelState.Name = "lbState1";
            this.labelState.Size = new System.Drawing.Size(92, 15);
            this.labelState.TabIndex = 5;
            this.labelState.Text = "disconnected";
            this.labelState.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // cbService1
            // 
            this.checkBoxService.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBoxService.AutoSize = true;
            this.checkBoxService.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.checkBoxService.Location = new System.Drawing.Point(415, 57);
            this.checkBoxService.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxService.Name = "cbService1";
            this.checkBoxService.Size = new System.Drawing.Size(65, 17);
            this.checkBoxService.TabIndex = 5;
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
            this.label1.TabIndex = 6;
            this.label1.Text = "Catalog host";
            // 
            // tbCatalogHost
            // 
            this.textBoxCatalogHost.Location = new System.Drawing.Point(72, 54);
            this.textBoxCatalogHost.Margin = new System.Windows.Forms.Padding(2);
            this.textBoxCatalogHost.Name = "tbCatalogHost";
            this.textBoxCatalogHost.Size = new System.Drawing.Size(86, 20);
            this.textBoxCatalogHost.TabIndex = 7;
            this.textBoxCatalogHost.Text = "localhost";
            // 
            // FrmClient
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 361);
            this.Controls.Add(this.textBoxCatalogHost);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.checkBoxService);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.labelClient);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "FrmClient";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmClient_FormClosing);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            //((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
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
    private System.Windows.Forms.CheckBox ccheckBoxSpeedTest;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.TextBox textBoxCatalogHost;
  }
}


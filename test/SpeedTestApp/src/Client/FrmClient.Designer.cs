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
            this.lbClient = new System.Windows.Forms.Label();
            this.lbService1 = new System.Windows.Forms.Label();
            this.tbService1 = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.cbSpeedTest1 = new System.Windows.Forms.CheckBox();
            this.lbState1 = new System.Windows.Forms.Label();
            this.cbService1 = new System.Windows.Forms.CheckBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.tbCatalogHost = new System.Windows.Forms.TextBox();
            //((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lbClient
            // 
            this.lbClient.AutoSize = true;
            this.lbClient.Location = new System.Drawing.Point(2, 6);
            this.lbClient.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lbClient.Name = "lbClient";
            this.lbClient.Size = new System.Drawing.Size(33, 13);
            this.lbClient.TabIndex = 0;
            this.lbClient.Text = "Client";
            // 
            // lbService1
            // 
            this.lbService1.AutoSize = true;
            this.lbService1.Location = new System.Drawing.Point(0, 2);
            this.lbService1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lbService1.Name = "lbService1";
            this.lbService1.Size = new System.Drawing.Size(43, 13);
            this.lbService1.TabIndex = 3;
            this.lbService1.Text = "Service";
            // 
            // tbService1
            // 
            this.tbService1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbService1.Location = new System.Drawing.Point(0, 57);
            this.tbService1.Margin = new System.Windows.Forms.Padding(2);
            this.tbService1.Multiline = true;
            this.tbService1.Name = "tbService1";
            this.tbService1.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbService1.Size = new System.Drawing.Size(481, 202);
            this.tbService1.TabIndex = 4;
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
            this.splitContainer1.Panel2.Controls.Add(this.cbSpeedTest1);
            this.splitContainer1.Panel2.Controls.Add(this.lbState1);
            this.splitContainer1.Panel2.Controls.Add(this.lbService1);
            this.splitContainer1.Panel2.Controls.Add(this.tbService1);
            this.splitContainer1.Size = new System.Drawing.Size(481, 283);
            this.splitContainer1.SplitterDistance = 27;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 5;
            // 
            // cbSpeedTest1
            // 
            this.cbSpeedTest1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbSpeedTest1.AutoSize = true;
            this.cbSpeedTest1.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.cbSpeedTest1.Location = new System.Drawing.Point(397, 36);
            this.cbSpeedTest1.Margin = new System.Windows.Forms.Padding(2);
            this.cbSpeedTest1.Name = "cbSpeedTest1";
            this.cbSpeedTest1.Size = new System.Drawing.Size(81, 17);
            this.cbSpeedTest1.TabIndex = 6;
            this.cbSpeedTest1.Text = "Speed Test";
            this.cbSpeedTest1.UseVisualStyleBackColor = true;
            // 
            // lbState1
            // 
            this.lbState1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lbState1.Location = new System.Drawing.Point(389, 19);
            this.lbState1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lbState1.Name = "lbState1";
            this.lbState1.Size = new System.Drawing.Size(92, 15);
            this.lbState1.TabIndex = 5;
            this.lbState1.Text = "disconnected";
            this.lbState1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // cbService1
            // 
            this.cbService1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbService1.AutoSize = true;
            this.cbService1.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.cbService1.Location = new System.Drawing.Point(415, 57);
            this.cbService1.Margin = new System.Windows.Forms.Padding(2);
            this.cbService1.Name = "cbService1";
            this.cbService1.Size = new System.Drawing.Size(65, 17);
            this.cbService1.TabIndex = 5;
            this.cbService1.Text = "connect";
            this.cbService1.UseVisualStyleBackColor = true;
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
            this.tbCatalogHost.Location = new System.Drawing.Point(72, 54);
            this.tbCatalogHost.Margin = new System.Windows.Forms.Padding(2);
            this.tbCatalogHost.Name = "tbCatalogHost";
            this.tbCatalogHost.Size = new System.Drawing.Size(86, 20);
            this.tbCatalogHost.TabIndex = 7;
            this.tbCatalogHost.Text = "localhost";
            // 
            // FrmClient
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 361);
            this.Controls.Add(this.tbCatalogHost);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbService1);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.lbClient);
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

    private System.Windows.Forms.Label lbClient;
    private System.Windows.Forms.Label lbService1;
    private System.Windows.Forms.TextBox tbService1;
    private System.Windows.Forms.SplitContainer splitContainer1;
    private System.Windows.Forms.CheckBox cbService1;
    private System.Windows.Forms.Timer timer1;
    private System.Windows.Forms.Label lbState1;
    private System.Windows.Forms.CheckBox cbSpeedTest1;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.TextBox tbCatalogHost;
  }
}


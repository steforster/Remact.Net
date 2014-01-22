namespace Test2.ClientAsyncAwait
{
  partial class FrmClientAsyncAwait
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
            this.lbService0 = new System.Windows.Forms.Label();
            this.tbService0 = new System.Windows.Forms.TextBox();
            this.lbService1 = new System.Windows.Forms.Label();
            this.tbService1 = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lbState0 = new System.Windows.Forms.Label();
            this.cbService0 = new System.Windows.Forms.CheckBox();
            this.cbSpeedTest1 = new System.Windows.Forms.CheckBox();
            this.lbState1 = new System.Windows.Forms.Label();
            this.cbService1 = new System.Windows.Forms.CheckBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.tbServiceHost = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lbClient
            // 
            this.lbClient.AutoSize = true;
            this.lbClient.Location = new System.Drawing.Point(3, 8);
            this.lbClient.Name = "lbClient";
            this.lbClient.Size = new System.Drawing.Size(72, 17);
            this.lbClient.TabIndex = 0;
            this.lbClient.Text = "Client new";
            // 
            // lbService0
            // 
            this.lbService0.AutoSize = true;
            this.lbService0.Location = new System.Drawing.Point(0, 0);
            this.lbService0.Name = "lbService0";
            this.lbService0.Size = new System.Drawing.Size(78, 17);
            this.lbService0.TabIndex = 1;
            this.lbService0.Text = "Service old";
            // 
            // tbService0
            // 
            this.tbService0.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbService0.Location = new System.Drawing.Point(0, 59);
            this.tbService0.Multiline = true;
            this.tbService0.Name = "tbService0";
            this.tbService0.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbService0.Size = new System.Drawing.Size(506, 88);
            this.tbService0.TabIndex = 2;
            // 
            // lbService1
            // 
            this.lbService1.AutoSize = true;
            this.lbService1.Location = new System.Drawing.Point(0, 2);
            this.lbService1.Name = "lbService1";
            this.lbService1.Size = new System.Drawing.Size(84, 17);
            this.lbService1.TabIndex = 3;
            this.lbService1.Text = "Service new";
            // 
            // tbService1
            // 
            this.tbService1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbService1.Location = new System.Drawing.Point(0, 70);
            this.tbService1.Multiline = true;
            this.tbService1.Name = "tbService1";
            this.tbService1.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbService1.Size = new System.Drawing.Size(506, 106);
            this.tbService1.TabIndex = 4;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(3, 92);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lbState0);
            this.splitContainer1.Panel1.Controls.Add(this.cbService0);
            this.splitContainer1.Panel1.Controls.Add(this.lbService0);
            this.splitContainer1.Panel1.Controls.Add(this.tbService0);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.cbSpeedTest1);
            this.splitContainer1.Panel2.Controls.Add(this.lbState1);
            this.splitContainer1.Panel2.Controls.Add(this.cbService1);
            this.splitContainer1.Panel2.Controls.Add(this.lbService1);
            this.splitContainer1.Panel2.Controls.Add(this.tbService1);
            this.splitContainer1.Size = new System.Drawing.Size(506, 322);
            this.splitContainer1.SplitterDistance = 147;
            this.splitContainer1.TabIndex = 5;
            // 
            // lbState0
            // 
            this.lbState0.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lbState0.Location = new System.Drawing.Point(355, 22);
            this.lbState0.Name = "lbState0";
            this.lbState0.Size = new System.Drawing.Size(148, 16);
            this.lbState0.TabIndex = 4;
            this.lbState0.Text = "disconnected";
            this.lbState0.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // cbService0
            // 
            this.cbService0.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbService0.AutoSize = true;
            this.cbService0.Location = new System.Drawing.Point(459, 0);
            this.cbService0.Name = "cbService0";
            this.cbService0.Size = new System.Drawing.Size(47, 21);
            this.cbService0.TabIndex = 3;
            this.cbService0.Text = "S0";
            this.cbService0.UseVisualStyleBackColor = true;
            // 
            // cbSpeedTest1
            // 
            this.cbSpeedTest1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbSpeedTest1.AutoSize = true;
            this.cbSpeedTest1.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.cbSpeedTest1.Location = new System.Drawing.Point(374, 48);
            this.cbSpeedTest1.Name = "cbSpeedTest1";
            this.cbSpeedTest1.Size = new System.Drawing.Size(103, 21);
            this.cbSpeedTest1.TabIndex = 6;
            this.cbSpeedTest1.Text = "Speed Test";
            this.cbSpeedTest1.UseVisualStyleBackColor = true;
            // 
            // lbState1
            // 
            this.lbState1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lbState1.Location = new System.Drawing.Point(380, 30);
            this.lbState1.Name = "lbState1";
            this.lbState1.Size = new System.Drawing.Size(122, 18);
            this.lbState1.TabIndex = 5;
            this.lbState1.Text = "disconnected";
            this.lbState1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // cbService1
            // 
            this.cbService1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbService1.AutoSize = true;
            this.cbService1.Location = new System.Drawing.Point(459, 2);
            this.cbService1.Name = "cbService1";
            this.cbService1.Size = new System.Drawing.Size(47, 21);
            this.cbService1.TabIndex = 5;
            this.cbService1.Text = "S1";
            this.cbService1.UseVisualStyleBackColor = true;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 70);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 17);
            this.label1.TabIndex = 6;
            this.label1.Text = "Host";
            // 
            // tbServiceHost
            // 
            this.tbServiceHost.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Test2.ClientAsyncAwait.Properties.Settings.Default, "ServiceHost", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.tbServiceHost.Location = new System.Drawing.Point(78, 67);
            this.tbServiceHost.Name = "tbServiceHost";
            this.tbServiceHost.Size = new System.Drawing.Size(114, 22);
            this.tbServiceHost.TabIndex = 7;
            this.tbServiceHost.Text = global::Test2.ClientAsyncAwait.Properties.Settings.Default.ServiceHost;
            // 
            // FrmClientAsyncAwait
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(511, 418);
            this.Controls.Add(this.tbServiceHost);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.lbClient);
            this.Name = "FrmClientAsyncAwait";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmClient_FormClosing);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.Label lbClient;
    private System.Windows.Forms.Label lbService0;
    private System.Windows.Forms.TextBox tbService0;
    private System.Windows.Forms.Label lbService1;
    private System.Windows.Forms.TextBox tbService1;
    private System.Windows.Forms.SplitContainer splitContainer1;
    private System.Windows.Forms.CheckBox cbService0;
    private System.Windows.Forms.CheckBox cbService1;
    private System.Windows.Forms.Timer timer1;
    private System.Windows.Forms.Label lbState0;
    private System.Windows.Forms.Label lbState1;
    private System.Windows.Forms.CheckBox cbSpeedTest1;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.TextBox tbServiceHost;
  }
}


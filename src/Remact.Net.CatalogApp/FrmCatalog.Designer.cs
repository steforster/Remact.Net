namespace Remact.Net.CatalogApp
{
  partial class FrmCatalog
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container ();
      this.tbStatus = new System.Windows.Forms.TextBox ();
      this.timer1 = new System.Windows.Forms.Timer (this.components);
      this.SuspendLayout ();
      // 
      // tbStatus
      // 
      this.tbStatus.Dock = System.Windows.Forms.DockStyle.Fill;
      this.tbStatus.Location = new System.Drawing.Point (0, 0);
      this.tbStatus.Multiline = true;
      this.tbStatus.Name = "tbStatus";
      this.tbStatus.ScrollBars = System.Windows.Forms.ScrollBars.Both;
      this.tbStatus.Size = new System.Drawing.Size (890, 166);
      this.tbStatus.TabIndex = 0;
      // 
      // timer1
      // 
      this.timer1.Enabled = true;
      this.timer1.Interval = 1000;
      this.timer1.Tick += new System.EventHandler (this.timer1_Tick);
      // 
      // FrmCatalog
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF (8F, 16F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size (890, 166);
      this.Controls.Add (this.tbStatus);
      this.Name = "FrmCatalog";
      this.ResumeLayout (false);
      this.PerformLayout ();

    }

    #endregion

    private System.Windows.Forms.TextBox tbStatus;
    private System.Windows.Forms.Timer timer1;
  }
}


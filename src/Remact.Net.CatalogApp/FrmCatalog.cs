
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Windows.Forms;
using System.Net;                  // Dns

namespace Remact.Net.CatalogApp
{
    /// <summary>
    /// Main form of the application.
    /// </summary>
    public partial class FrmCatalog: Form
  {
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the FrmCatalog class.
    /// </summary>
    public FrmCatalog()
    {
      InitializeComponent();
      this.Text = "Remact.Catalog on " + Dns.GetHostName();
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Notification Handlers

    private int  m_Seconds=0;
    //---------------------------------------------------
    private void timer1_Tick (object sender, EventArgs e)
    {
      timer1.Stop ();
      if (m_Seconds % 5 == 0) RaLog.Run ();
      m_Seconds++;

      try
      {
        Program.Catalog.PeriodicCall (1, tbStatus);
      }
      catch (Exception ex)
      {
        RaLog.Exception ("during timerevent", ex);
        tbStatus.Text = ex.Message;
      }
      timer1.Start ();
    }

    #endregion
  }
}
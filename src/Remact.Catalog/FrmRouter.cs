
// Copyright (c) 2012  AsyncWcfLib.sourceforge.net

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;                  // Dns

namespace SourceForge.AsyncWcfLib
{
  /// <summary>
  /// Represents ...
  /// </summary>
  public partial class FrmRouter: Form
  {
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the FrmRouter class.
    /// </summary>
    public FrmRouter()
    {
      InitializeComponent();
      this.Text = "AsyncWcfLib.Router on "+Dns.GetHostName ();
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Event Handlers

    private int  m_Seconds=0;
    //---------------------------------------------------
    private void timer1_Tick (object sender, EventArgs e)
    {
      timer1.Stop ();
      if (m_Seconds % 5 == 0) WcfTrc.Run ();
      m_Seconds++;

      try
      {
        Program.Router.PeriodicCall (1, tbStatus);
      }
      catch (Exception ex)
      {
        WcfTrc.Exception ("during timerevent", ex);
        tbStatus.Text = ex.Message;
      }
      timer1.Start ();
    }

    #endregion
  }
}
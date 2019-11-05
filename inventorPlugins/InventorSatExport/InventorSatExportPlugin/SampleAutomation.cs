/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Inventor;
using Autodesk.Forge.DesignAutomation.Inventor.Utils;
using System.Text;

namespace InventorSatExportPlugin
{
    [ComVisible(true)]
    public class SampleAutomation
    {
        private readonly InventorServer inventorApplication;

        public SampleAutomation(InventorServer inventorApp)
        {
            inventorApplication = inventorApp;
        }

        public void Run(Document doc)
        {
            LogTrace("Start Processing Run method");
            NameValueMap map = inventorApplication.TransientObjects.CreateNameValueMap();
            RunWithArguments(doc, map);
        }

        public void RunWithArguments(Document doc, NameValueMap map)
        {
            LogTrace("Processing " + doc.FullFileName);

            try
            {
                //string dirPath = System.IO.Path.GetDirectoryName(doc.FullDocumentName);
                string currentDirPath = System.IO.Directory.GetCurrentDirectory();

                #region ExportSAT file 

                Trace.TraceInformation("Export SAT file.");
                TranslatorAddIn oSAT = null;

                foreach (ApplicationAddIn item in inventorApplication.ApplicationAddIns)
                {

                    if (item.ClassIdString == "{89162634-02B6-11D5-8E80-0010B541CD80}")
                    {
                        Trace.TraceInformation("Finded the PDF addin.");
                        oSAT = (TranslatorAddIn)item;
                        break;
                    }
                    else { }
                }

                if (oSAT != null)
                {
                    TranslationContext oContext = inventorApplication.TransientObjects.CreateTranslationContext();
                    NameValueMap oIgesMap = inventorApplication.TransientObjects.CreateNameValueMap();

                    if (oSAT.get_HasSaveCopyAsOptions(doc, oContext, oIgesMap))
                    {
                        Trace.TraceInformation("SAT can be exported.");

                        Trace.TraceInformation("SAT: Set context type");
                        oContext.Type = IOMechanismEnum.kFileBrowseIOMechanism;

                        Trace.TraceInformation("SAT: create data medium");
                        DataMedium oData = inventorApplication.TransientObjects.CreateDataMedium();

                        Trace.TraceInformation("SAT save to: " + currentDirPath + "\\export.sat");
                        oData.FileName = currentDirPath + "\\export.sat";

                        oIgesMap.set_Value("GeometryType", 1);

                        oSAT.SaveCopyAs(doc, oContext, oIgesMap, oData);
                        Trace.TraceInformation("SAT exported.");
                    }

                    #endregion
                    GetListOfDirectory(System.IO.Directory.GetCurrentDirectory());
                }

            }
            catch (Exception e)
            {
                LogError("Processing failed. " + e.ToString());
            }
        }

        public void GetListOfDirectory(string dirPath)
        {
            string[] directories = System.IO.Directory.GetDirectories(dirPath);
            string[] files = System.IO.Directory.GetFiles(dirPath);
            LogTrace(" List Of Directory : " + dirPath);
            foreach (string directory in directories)
            {
                LogTrace("Dir: " + directory);
            }

            foreach (string file in files)
            {
                LogTrace("File: " + file);
            }
            LogTrace("-----------------------------------------------------------");
        }

        #region Logging utilities

        /// <summary>
        /// Log message with 'trace' log level.
        /// </summary>
        private static void LogTrace(string format, params object[] args)
        {
            Trace.TraceInformation(format, args);
        }

        /// <summary>
        /// Log message with 'trace' log level.
        /// </summary>
        private static void LogTrace(string message)
        {
            Trace.TraceInformation(message);
        }

        /// <summary>
        /// Log message with 'error' log level.
        /// </summary>
        private static void LogError(string format, params object[] args)
        {
            Trace.TraceError(format, args);
        }

        /// <summary>
        /// Log message with 'error' log level.
        /// </summary>
        private static void LogError(string message)
        {
            Trace.TraceError(message);
        }

        #endregion
    }
}
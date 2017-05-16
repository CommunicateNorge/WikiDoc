using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace TPDocWeb.Controllers
{
    /// <summary>
    /// Handles logging of errors
    /// </summary>
    public class ErrorLogger : HandleErrorAttribute
    {
        /// <summary>
        /// -- LOGS ERRORS --
        /// Called when an uncaught exception is thrown from an action/controller
        /// that uses the [LogErrorFilter].
        /// (This method should never be called explicitly)
        /// </summary>
        /// <param name="cntx">The exception context</param>
        public override void OnException(ExceptionContext cntx)
        {
            base.OnException(cntx);

            if (cntx.Exception != null)
            {
#if DEBUG
                AzureStorage.AzureBlobStorage azure = new AzureStorage.AzureBlobStorage(AzureStorage.AzureStorageInfo.WikiFilesSecondary, false);
#else
                AzureStorage.AzureBlobStorage azure = new AzureStorage.AzureBlobStorage(AzureStorage.AzureStorageInfo.WikiFilesPrimary, false);
#endif
                DateTime d = DateTime.Now;
                azure.SetBlobContentAsString("Exception£Log£" + d.Year + "€" + d.Month + "€" + d.Day + "£" + d.Hour + "€" + d.Minute + "£" + Guid.NewGuid(), cntx.Exception.ToString());
            }
            cntx.ExceptionHandled = false;
        }

    }
}
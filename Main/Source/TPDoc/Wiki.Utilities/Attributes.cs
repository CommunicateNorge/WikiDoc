using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Wiki.Utilities
{
    public class AjaxAuthorizeAttribute : AuthorizeAttribute
    {
        protected override void HandleUnauthorizedRequest(AuthorizationContext context)
        {
            if (context.HttpContext.Request.IsAjaxRequest())
            {
                var urlHelper = new UrlHelper(context.RequestContext);
                context.HttpContext.Response.StatusCode = 403;
                context.Result = new JsonResult
                {
                    Data = new
                    {
                        Error = "NotAuthorized",
                        LogOnUrl = urlHelper.Action("SingIn", "Account")
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                base.HandleUnauthorizedRequest(context);
            }
        }
    }

    //[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    //public sealed class AjaxAuthorizeAttribute : AuthorizeAttribute
    //{
    //    public override void OnAuthorization(AuthorizationContext filterContext)
    //    {
    //        base.OnAuthorization(filterContext);
    //        OnAuthorizationHelp(filterContext);
    //    }

    //    internal void OnAuthorizationHelp(AuthorizationContext filterContext)
    //    {

    //        if (filterContext.Result is HttpUnauthorizedResult)
    //        {
    //            if (filterContext.HttpContext.Request.IsAjaxRequest())
    //            {
    //                filterContext.HttpContext.Response.StatusCode = 401;
    //                filterContext.HttpContext.Response.End();
    //            }
    //        }
    //    }
    //}
}

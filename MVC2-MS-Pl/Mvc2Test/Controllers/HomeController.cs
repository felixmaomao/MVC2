using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;  //注意我们这边用的是mvc2 进行的测试
using Mvc2Test.Services;
namespace Mvc2Test.Controllers
{

    public class HomeController : Controller
    {
        //
        // GET: /Home/

      
        //经过测试，确实验证了我们的猜想。在mvc2.0中，默认工厂创建controller实例是必须要有无参构造函数的，默认工厂是通过无参构造函数的反射来创建controller的。
        //所以在mvc4中针对这样的局限，做了很大的改进。
        public HomeController(Logger logger) 
        {
            logger.writelog();
        }

        
        public void sayhi()
        {
            this.Response.Write("Hi This Is MVC2 Test");
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BatchSample.Controllers;
using BatchSample.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BatchSample.Tests.Controllers
{
    [TestClass]
    public class UserControllerTest
    {
        [TestMethod]
        public void Create()
        {
            var controller = new UserController();
            var username = "george";

            var result = controller.Create(new User() { Username = username});

            Assert.IsNotNull(result);
            Assert.AreEqual(result.Username, username);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using BatchSample.Models;
using Microsoft.Win32;
using WebGrease.Css.Ast.Selectors;

namespace BatchSample.Controllers
{
    public class UserController : ApiController
    {
        [HttpPost]
        public User Create([FromBody]User user)
        {
            return new User {Username = user.Username, FirstName = user.FirstName, LastName = user.LastName};
        }

    }
}
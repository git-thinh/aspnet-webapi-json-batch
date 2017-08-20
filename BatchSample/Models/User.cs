using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BatchSample.Models
{
    public class User
    {
        public string Username { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public DateTime DateCreated { get; set; }

        public User()
        {
            DateCreated = DateTime.Now;
        }
    }
}
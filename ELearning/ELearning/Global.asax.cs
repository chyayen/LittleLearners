using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace ELearning
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            //check for bin files to be loaded
            CheckAddBinPath();
        }

        public static void CheckAddBinPath()
        {
            // find path to 'bin' folder
            var binPath = Path.Combine(new string[]
                { AppDomain.CurrentDomain.BaseDirectory, "bin" });
            // get current search path from environment
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";

            // add 'bin' folder to search path if not already present
            if (!path.Split(Path.PathSeparator).Contains(binPath, StringComparer.CurrentCultureIgnoreCase))
            {
                path = string.Join(Path.PathSeparator.ToString
                    (CultureInfo.InvariantCulture), new string[] { path, binPath });
                Environment.SetEnvironmentVariable("PATH", path);
            }
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            //// Ensure session is available and only update for authenticated users
            //if (Session != null)
            //{
            //    // Example: Update countStudent for all user page requests
            //    GetStudentCount();
            //}
        }

        // Example method to get student count (replace with actual logic)
        private void GetStudentCount()
        { 
            MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT (select COUNT(id) from users where usertype = 'student' and isverified != 1) as cntNotVerifyStudents, (select COUNT(id) from users where usertype = 'teacher' and isverified != 1) as cntNotVerifyTeachers;"; 

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    Session["CountNotVeriedStudents"] = rd["cntNotVerifyStudents"] != null && rd["cntNotVerifyStudents"].ToString() != "" ? Convert.ToInt32(rd["cntNotVerifyStudents"].ToString()) : 0;
                    Session["CountNotVeriedTeachers"] = rd["cntNotVerifyTeachers"] != null && rd["cntNotVerifyTeachers"].ToString() != "" ? Convert.ToInt32(rd["cntNotVerifyTeachers"].ToString()) : 0;
                    rd.Close();
                }
            }
            finally
            {
                con.Close();
            } 
        }


    }
}

using ELearning.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ELearning.Controllers
{
    public class AccountController : Controller
    {
        private string usertype = "student";
        private string defaultConnection = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        // GET: Account
        public ActionResult Index()
        {
            if (Session["UserName"] != null)
            {
                return View();
            }
            else
            {
                // Store the current URL (for redirecting after login) 
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }
        }

        [HttpPost] 
        public ActionResult AccountUpdateProfile(string fullName, string email, HttpPostedFileBase defaultImage)
        {
            if (Session["UserName"] != null)
            {
                int userid = (int)Session["UserID"];
                var username = Session["UserName"];
                var defaultImageName = "";
                if (defaultImage != null && defaultImage.ContentLength > 0)
                {
                    string filetype = Path.GetExtension(defaultImage.FileName);
                    if (filetype.Contains(".jpg") || filetype.Contains(".jpeg") || filetype.Contains(".png"))
                    {
                        defaultImageName = username + filetype;
                    }
                } 
                 
                if(ProfileUpdate(userid, fullName, email, defaultImageName) > 0)
                {
                    if (defaultImage != null && defaultImage.ContentLength > 0)
                    {
                        string path = Server.MapPath("~/Images/Users/" + defaultImageName);
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);

                        defaultImage.SaveAs(path);
                        Session["DefaultImageName"] = defaultImageName;
                    }

                    Session["FullName"] = fullName;
                    Session["Email"] = email; 
                }

                return RedirectToAction("Index");
            }
            else
            {
                // Store the current URL (for redirecting after login) 
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }
        }

        [HttpPost] 
        public ActionResult AccountUpdatePassword(FormCollection form)
        {
            if (Session["UserName"] != null)
            {
                var currentPassword = form["CurrentPassword"];
                var newPassword = form["NewPassword"];
                var confirmNewPassword = form["ConfirmNewPassword"];
                int userid = (int)Session["UserID"];

                if (newPassword == confirmNewPassword)
                {
                    if(PasswordUpdate(userid, currentPassword, newPassword) > 0)
                    {
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    TempData["AlertMessage"] = "<p class='alert alert-danger'>New passwords do not match.</p>";
                }

                return RedirectToAction("Index");
            }
            else
            {
                // Store the current URL (for redirecting after login) 
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }
        }

        public ActionResult Login(string returnUrl)
        { 
            if (Session["UserName"] != null)
            {
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            return View(new LoginViewModel() { ReturnURL = returnUrl }); // Pass returnUrl to the view for use
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            var userdb = GetUserLogin(model.UserName, model.Password);

            if (userdb != null && !string.IsNullOrEmpty(userdb.UserName))
            {
                var lockOutEndTime = userdb.DateLocked.AddMinutes(30);
                if(lockOutEndTime > DateTime.Now)
                {
                    TempData["AlertMessage"] = $"<p class='alert alert-danger'>Account locked. Try again after {lockOutEndTime.ToString("g")}</p>";
                    return View(model); 
                }

                if (userdb.IsVerified)
                {
                    ResetLoginAttempt(model.UserName, model.Password);

                    Session.Timeout = 3600;
                    Session["UserID"] = userdb.UserID;
                    Session["UserName"] = userdb.UserName;
                    Session["FullName"] = userdb.FullName;
                    Session["Email"] = userdb.Email;
                    Session["UserType"] = userdb.UserType;
                    Session["ClassID"] = userdb.ClassID;
                    Session["ClassName"] = userdb.ClassName;
                    Session["SectionID"] = userdb.SectionID;
                    Session["SectionName"] = userdb.SectionName;
                    Session["DefaultImageName"] = userdb.DefaultImageName;
                    Session["TeacherName"] = userdb.TeacherName;
                    Session["IsLock"] = userdb.IsLock;
                    Session["DateLocked"] = userdb.DateLocked;

                    if (!string.IsNullOrEmpty(model.ReturnURL))
                    {
                        return Redirect(model.ReturnURL);
                    }
                    else
                    {
                        return RedirectToAction("Index", "Story");
                    } 
                }
                else
                {
                    TempData["AlertMessage"] = "<p class='alert alert-danger'>Your account needs verification. Please wait or contact the school administrator for your account to be verified.</p>";
                }
            }
            else
            {
                SaveLoginAttempt(model.UserName);
                TempData["AlertMessage"] = "<p class='alert alert-danger'>Incorrect username or password.</p>";
            }

            return View(model);
        }

        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public ActionResult Logout(LoginViewModel model)
        {
            Session.Abandon(); // it will clear the session at the end of request
            return RedirectToAction("Login");
        }

        public ActionResult Register()
        {
            RegisterViewModel model = new RegisterViewModel();
            model.UserType = usertype;
            model.ClassList = GetAllClasses().AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (UserInsertDB(model) > 0)
            {
                TempData["AlertMessage"] = "<p class='alert alert-success'>Your account was successfully created. Please wait or contact the school administrator for your account to be verified.</p>";

                model.FullName = "";
                model.Email = "";
                model.ClassID = 0;
                model.UserName = "";
            }

            model.ClassList = GetAllClasses().AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }

        // This action returns the list of sections based on the selected ClassID
        public JsonResult GetSectionsByClass(int classId)
        {
            // Fetch sections based on classId from your database
            var sections = GetAllSectionsByClassIDDB(classId).AsEnumerable()
                                      .Select(s => new { SectionID = s.ID, SectionName = s.Name })
                                      .ToList();
            return Json(sections, JsonRequestBehavior.AllowGet);
        }

        // This action returns the list of sections based on the selected ClassID and TeacherID
        public JsonResult GetSectionsByTeacherAndClass(int classid, int teacherid)
        {
            // Fetch sections based on classId from your database
            var sections = GetAllSectionsByTeacherAndClassDB(classid, teacherid).AsEnumerable()
                                      .Select(s => new { SectionID = s.ID, SectionName = s.Name })
                                      .ToList();
            return Json(sections, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetTeachersByClassID(int classid)
        {
            // Fetch sections based on classId from your database
            var sections = GetAllTeachersByClassIDDB(classid).AsEnumerable()
                                      .Select(s => new { TeacherID = s.ID, TeacherName = s.Name })
                                      .ToList();
            return Json(sections, JsonRequestBehavior.AllowGet);
        }





        #region Helpers
        private LoginViewModel GetUserLogin(string username, string password)
        {
            LoginViewModel model = new LoginViewModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select u.*, sc.classid, c.name as classname, sc.sectionid, s.name as sectionname, t.name as teachername
                                from users u 
                                left join studentclasses sc on sc.studentid = u.id 
                                left join sections s on s.id = sc.sectionid
                                left join classes c on c.id = sc.classid
                                left join users t on t.id = s.teacherid
                                where u.username = @user and u.password = @pass and u.usertype = @logintype";
            cmd.Parameters.AddWithValue("@user", username);
            cmd.Parameters.AddWithValue("@pass", password);
            cmd.Parameters.AddWithValue("@logintype", usertype);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    model.UserID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.UserName = rd["username"] != null ? rd["username"].ToString() : "";
                    model.Password = rd["password"] != null ? rd["password"].ToString() : "";
                    model.IsVerified = rd["isverified"] != null ? (bool)rd["isverified"] : false;
                    model.FullName = rd["name"] != null ? rd["name"].ToString() : "";
                    model.Email = rd["email"] != null ? rd["email"].ToString() : "";
                    model.UserType = rd["usertype"] != null ? rd["usertype"].ToString() : "";
                    model.ClassID = rd["classid"] != null && rd["classid"].ToString() != "" ? Convert.ToInt32(rd["classid"].ToString()) : 0;
                    model.ClassName = rd["classname"] != null ? rd["classname"].ToString() : "";
                    model.SectionID = rd["sectionid"] != null && rd["sectionid"].ToString() != "" ? Convert.ToInt32(rd["sectionid"].ToString()) : 0;
                    model.SectionName = rd["sectionname"] != null ? rd["sectionname"].ToString() : "";
                    model.DefaultImageName = rd["defaultimagename"] != null ? rd["defaultimagename"].ToString() : "";
                    model.IsActive = rd["isactive"] != null ? (bool)rd["isactive"] : false; 
                    model.TeacherName = rd["teachername"] != null ? rd["teachername"].ToString() : "";
                    model.IsLock = rd["islock"] != null ? (bool)rd["islock"] : false;
                    model.DateLocked = rd["datelocked"] != null && rd["datelocked"].ToString() != "" ? Convert.ToDateTime(rd["datelocked"].ToString()) : new DateTime(2000, 1, 1);
                    rd.Close();
                }
            }
            catch (Exception ex)
            {
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }
            finally
            {
                con.Close();
            }
            return model;
        }

        private int UserInsertDB(RegisterViewModel model)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"INSERT INTO users(username, name, email, password, isverified, usertype, defaultimagename, isactive) 
                                VALUES(@username, @name, @email, @password, @isverified, @usertype, '', 1);

                                INSERT INTO studentclasses(studentid, classid, sectionid)
                                VALUES(LAST_INSERT_ID(), @classid, @sectionid);";
            cmd.Parameters.AddWithValue("@username", model.UserName);
            cmd.Parameters.AddWithValue("@name", model.FullName);
            cmd.Parameters.AddWithValue("@email", model.Email);
            cmd.Parameters.AddWithValue("@password", model.Password);
            cmd.Parameters.AddWithValue("@usertype", usertype);
            cmd.Parameters.AddWithValue("@isverified", false);
            cmd.Parameters.AddWithValue("@classid", model.ClassID);
            cmd.Parameters.AddWithValue("@sectionid", model.SectionID);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                if (count > 0)
                {
                    TempData["AlertMessage"] = "<p class='alert alert-success'>Your registration is successful.</p>";
                } 
            }
            catch (Exception ex)
            {
                if (ex.Message != null && ex.Message.ToLower().Contains("duplicate") && ex.Message.ToLower().Contains("username"))
                {
                    TempData["AlertMessage"] = "<p class='alert alert-danger'>Username already exist.</p>";
                }
                else
                {
                    TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
                }
                
            }
            finally
            {
                con.Close();
            }
            return count;

        }

        private int ResetLoginAttempt(string username, string password)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"update users set islock = 0, datelocked = null, login_attempt = 0
                                where username = @user and password = @pass and usertype = @logintype";
            cmd.Parameters.AddWithValue("@user", username);
            cmd.Parameters.AddWithValue("@pass", password);
            cmd.Parameters.AddWithValue("@logintype", usertype);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }
            finally
            {
                con.Close();
            }
            return count;
        }

        //private int LockUser(string username, DateTime datelocked)
        //{
        //    int count = 0;
        //    MySqlConnection con = new MySqlConnection(defaultConnection);
        //    MySqlCommand cmd = con.CreateCommand();
        //    cmd.CommandText = @"update users set islock = 1, datelocked = @datelocked 
        //                        where username = @user and usertype = @logintype";
        //    cmd.Parameters.AddWithValue("@user", username);
        //    cmd.Parameters.AddWithValue("@datelocked", datelocked);
        //    cmd.Parameters.AddWithValue("@logintype", usertype);

        //    try
        //    {
        //        con.Open();
        //        count = cmd.ExecuteNonQuery();
        //    }
        //    catch (Exception ex)
        //    {
        //        TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
        //    }
        //    finally
        //    {
        //        con.Close();
        //    }
        //    return count;
        //}

        private int SaveLoginAttempt(string username)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"update users set login_attempt = login_attempt + @login_attempt, islock = (case when login_attempt >= 5 then 1 else 0 end), datelocked = (case when login_attempt >= 5 then SYSDATE() else null end)
                                where username = @user and usertype = @logintype";
            cmd.Parameters.AddWithValue("@user", username);
            cmd.Parameters.AddWithValue("@login_attempt", 1);
            cmd.Parameters.AddWithValue("@logintype", usertype);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }
            finally
            {
                con.Close();
            }
            return count;
        }

        private List<ClassModel> GetAllClasses()
        {
            List<ClassModel> list = new List<ClassModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select * from classes";

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    ClassModel model = new ClassModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    list.Add(model);
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return list;
        }

        private int ProfileUpdate(int userid, string fullName, string email, string defaultImageName)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"UPDATE users SET name = @name, email = @email, defaultimagename = (case when @defaultimagename is null or @defaultimagename = '' then defaultimagename else @defaultimagename end)
                                WHERE id = @userid";
            cmd.Parameters.AddWithValue("@userid", userid);
            cmd.Parameters.AddWithValue("@name", fullName);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@defaultimagename", defaultImageName); 

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                if (count > 0)
                {
                    TempData["AlertMessage"] = "<p class='alert alert-success'>Your profile update is successful.</p>";
                }
            }
            catch (Exception ex)
            {
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }
            finally
            {
                con.Close();
            }
            return count;

        }
        private int PasswordUpdate(int userid, string currentPassword, string newPassword)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"UPDATE users SET password = @newPassword WHERE id = @userid and password = @currentPassword";
            cmd.Parameters.AddWithValue("@userid", userid);
            cmd.Parameters.AddWithValue("@newPassword", newPassword);
            cmd.Parameters.AddWithValue("@currentPassword", currentPassword);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                if (count > 0)
                {
                    TempData["AlertMessage"] = "<p class='alert alert-success'>Your password update is successful.</p>";
                }
                else
                {
                    TempData["AlertMessage"] = "<p class='alert alert-danger'>Your current password is incorrect.</p>";
                }
            }
            catch (Exception ex)
            {
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }
            finally
            {
                con.Close();
            }
            return count;

        }

        private List<TeacherEditViewModel> GetAllTeachersByClassIDDB(int classid)
        {
            List<TeacherEditViewModel> list = new List<TeacherEditViewModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select sec.classid, c.name as classname, sec.teacherid, u.name as teachername, sec.id as sectionid, sec.name as sectionname
                                from `users` u 
                                inner join `sections` sec on sec.teacherid = u.id
                                inner join `classes` c on sec.classid = c.id
                                where u.usertype = 'teacher' and sec.classid = @classid
                                order by u.name";
            cmd.Parameters.AddWithValue("@classid", classid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    TeacherEditViewModel model = new TeacherEditViewModel();
                    model.ID = rd["teacherid"] != null && rd["teacherid"].ToString() != "" ? Convert.ToInt32(rd["teacherid"].ToString()) : 0;
                    model.Name = rd["teachername"] != null ? rd["teachername"].ToString() : "";
                    list.Add(model);
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return list;
        }

        private List<SectionModel> GetAllSectionsByTeacherAndClassDB(int classid, int teacherid)
        {
            List<SectionModel> list = new List<SectionModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT * FROM `sections` sec WHERE sec.teacherid = @teacherid and sec.classid = @classid ORDER BY sec.name";
            cmd.Parameters.AddWithValue("@classid", classid);
            cmd.Parameters.AddWithValue("@teacherid", teacherid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    SectionModel model = new SectionModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    list.Add(model);
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return list;
        }
        private List<SectionModel> GetAllSectionsByClassIDDB(int classid)
        {
            List<SectionModel> list = new List<SectionModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select s.*, u.name as teachername from sections s left join users u on s.teacherid = u.id where s.classid = @classid";
            cmd.Parameters.AddWithValue("@classid", classid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    SectionModel model = new SectionModel();
                    var teachername = rd["teachername"] != null ? rd["teachername"].ToString() : "";
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Name = rd["name"] != null ? rd["name"].ToString() + (!string.IsNullOrEmpty(teachername) ? $" ({teachername})" : "") : "";
                    model.ClassID = rd["classid"] != null && rd["classid"].ToString() != "" ? Convert.ToInt32(rd["classid"].ToString()) : 0;
                    model.TeacherID = rd["teacherid"] != null && rd["teacherid"].ToString() != "" ? Convert.ToInt32(rd["teacherid"].ToString()) : 0;
                    list.Add(model);
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return list;
        }
        #endregion Helpers


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
        }

    }
}
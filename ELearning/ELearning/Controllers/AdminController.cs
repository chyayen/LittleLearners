using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ELearning.Models;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using PagedList;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data; 
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace ELearning.Controllers
{
    public class AdminController : Controller
    {
        private string usertype = "teacher";
        string defaultConnection = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
         
        public ActionResult Index()
        {
            if (Session["UserName"] != null)
            {
                if(Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    var userid = (int)Session["UserID"];
                    var usertype = Session["UserType"].ToString();
                    AdminViewModel model = GetUserDataForDashboard(userid, usertype); 
                    return View(model);
                }
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }
        }


        #region Login 
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
                    return RedirectToAction("Index");
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
                if (lockOutEndTime > DateTime.Now)
                {
                    TempData["AlertMessage"] = $"<p class='alert alert-danger'>Account locked. Try again after {lockOutEndTime.ToString("g")}</p>";
                    return View(model);
                }
                else
                {
                    ResetLoginAttempt(model.UserName, model.Password);
                }

                if (userdb.IsVerified)
                {
                    Session.Timeout = 3600;
                    Session["UserID"] = userdb.UserID;
                    Session["UserName"] = userdb.UserName;
                    Session["FullName"] = userdb.FullName;
                    Session["Email"] = userdb.Email;
                    Session["UserType"] = userdb.UserType;
                    Session["DefaultImageName"] = userdb.DefaultImageName;
                    Session["CountNotVeriedStudents"] = userdb.CountNotVeriedStudents;
                    Session["CountNotVeriedTeachers"] = userdb.CountNotVeriedTeachers;

                    if (!string.IsNullOrEmpty(model.ReturnURL))
                    {
                        return Redirect(model.ReturnURL);
                    }
                    else
                    {
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    TempData["AlertMessage"] = "<p class='alert alert-danger'>Your account needs verification. Please contact the school administrator or wait 2-3 days for your account to be verified.</p>";
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
            return RedirectToAction("Login", "Admin");
        }

        public ActionResult AccountSettings(int? id)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            return View();
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
                    string filetype = System.IO.Path.GetExtension(defaultImage.FileName);
                    if (filetype.Contains(".jpg") || filetype.Contains(".jpeg") || filetype.Contains(".png"))
                    {
                        defaultImageName = username + filetype;
                    }
                }

                if (ProfileUpdate(userid, fullName, email, defaultImageName) > 0)
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

                return RedirectToAction("AccountSettings", new { id = userid });
            }
            else
            {
                // Store the current URL (for redirecting after login) 
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
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
                    if (PasswordUpdate(userid, currentPassword, newPassword) > 0)
                    {
                        return RedirectToAction("AccountSettings", new { id = userid });
                    }
                }
                else
                {
                    TempData["AlertMessage"] = "<p class='alert alert-danger'>Passwords do not match.</p>";
                }

                return RedirectToAction("AccountSettings", new { id = userid });
            }
            else
            {
                // Store the current URL (for redirecting after login) 
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }
        }




        private LoginViewModel GetUserLogin(string username, string password)
        {
            LoginViewModel model = new LoginViewModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT u.*, sc.cntNotVerify, (select COUNT(id) from users where usertype = 'student' and isverified != 1) as cntNotVerifyStudents
                                    , (select COUNT(id) from users where usertype = 'teacher' and isverified != 1) as cntNotVerifyTeachers FROM `users` u
                                LEFT JOIN (
	                                select s.teacherid, COUNT(sc.studentid) as cntNotVerify from sections s
                                    inner join studentclasses sc on sc.sectionid = s.id
                                    inner join users u on u.id = sc.studentid and u.isverified != 1
                                    group by s.teacherid
                                ) sc on sc.teacherid = u.id
                                WHERE u.username = @username and u.password = @password";
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@password", password); 

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
                    model.DefaultImageName = rd["defaultimagename"] != null && rd["defaultimagename"].ToString() != "" ? rd["defaultimagename"].ToString() : "defaultstudentimage.png";
                    //model.CountNotVeriedStudents = rd["cntNotVerify"] != null && rd["cntNotVerify"].ToString() != "" ? Convert.ToInt32(rd["cntNotVerify"].ToString()) : 0;
                    model.CountNotVeriedStudents = rd["cntNotVerifyStudents"] != null && rd["cntNotVerifyStudents"].ToString() != "" ? Convert.ToInt32(rd["cntNotVerifyStudents"].ToString()) : 0;
                    model.CountNotVeriedTeachers = rd["cntNotVerifyTeachers"] != null && rd["cntNotVerifyTeachers"].ToString() != "" ? Convert.ToInt32(rd["cntNotVerifyTeachers"].ToString()) : 0;
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

        private AdminViewModel GetUserDataForDashboard(int userid, string usertype)
        {
            AdminViewModel model = new AdminViewModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (usertype == "teacher")
            {
                cmd.CommandText = @"select *, (select COUNT(*) from studentclasses sc inner join sections s on sc.sectionid = s.id and s.teacherid = u.id inner join users uu on uu.id = sc.studentid and uu.usertype = 'student') as TotalStudents 
	                                , (select COUNT(*) from stories s 
                                        left join classes c on s.classid = c.id 
                                        left join sections sec on sec.classid = c.id
                                        where (s.isdeleted is null or s.isdeleted = 0) and sec.teacherid = u.id) as TotalStories
                                    , (select COUNT(*) from users sc where sc.isactive = 1) as TotalUsers
                                   , (select COUNT(*) from users sc where sc.isactive = 1 and sc.usertype = 'teacher') as TotalTeachers
                                from users u
                                where u.id = @userid";
            }
            else
            {
                cmd.CommandText = @"select *, (select COUNT(*) from users uu where uu.isactive = 1 and uu.usertype = 'student') as TotalStudents 
	                                , (select COUNT(*) from stories s 
                                        left join classes c on s.classid = c.id 
                                        left join sections sec on sec.classid = c.id
                                        where (s.isdeleted is null or s.isdeleted = 0)) as TotalStories
                                    , (select COUNT(*) from users sc where sc.isactive = 1) as TotalUsers
                                   , (select COUNT(*) from users sc where sc.isactive = 1 and sc.usertype = 'teacher') as TotalTeachers
                                from users u
                                where u.id = @userid";
            }
            cmd.Parameters.AddWithValue("@userid", userid); 

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    model.TotalStudents = rd["TotalStudents"] != null && rd["TotalStudents"].ToString() != "" ? Convert.ToInt32(rd["TotalStudents"].ToString()) : 0; 
                    model.TotalStories = rd["TotalStories"] != null && rd["TotalStories"].ToString() != "" ? Convert.ToInt32(rd["TotalStories"].ToString()) : 0;
                    model.TotalUsers = rd["TotalUsers"] != null && rd["TotalUsers"].ToString() != "" ? Convert.ToInt32(rd["TotalUsers"].ToString()) : 0;
                    model.TotalTeachers = rd["TotalTeachers"] != null && rd["TotalTeachers"].ToString() != "" ? Convert.ToInt32(rd["TotalTeachers"].ToString()) : 0;
                    //model.StudentGradeList = GetStudentProgressForDashboard(userid);
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

        private List<StudentGradeModel> GetStudentProgressForDashboard(int userid)
        {
            List<StudentGradeModel> list = new List<StudentGradeModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select * from (
                                    select sg.studentid, u.username, sg.grade, sg.storyid, ROW_NUMBER() over(partition by sg.storyid order by attempt desc) as rownum
                                    from studentgrades sg
                                    inner join users u on u.id = sg.studentid
                                    inner join studentclasses sc on sc.studentid = u.id
                                    inner join sections s on sc.sectionid = s.id and s.teacherid = @userid
                                ) sg where sg.rownum = 1";
            cmd.Parameters.AddWithValue("@userid", userid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StudentGradeModel model = new StudentGradeModel();
                    int grade = rd["grade"] != null && rd["grade"].ToString() != "" ? Convert.ToInt32(rd["grade"].ToString()) : 0;
                    List<int> gradeList = new List<int>();
                    gradeList.Add(grade);

                    model.UserName = rd["username"] != null && rd["username"].ToString() != "" ? rd["username"].ToString() : "";
                    model.Grade = gradeList.ToArray();
                    list.Add(model);
                }
                rd.Close();
            }
            catch (Exception ex)
            {
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }
            finally
            {
                con.Close();
            }
            return list;
        }
        #endregion Login


        #region Register
        public ActionResult Register()
        {
            RegisterViewModel model = new RegisterViewModel();
            model.UserType = usertype;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (UserInsertDB(model) > 0)
            {
                return RedirectToAction("Login", "Admin");
            }

            return View(model);
        }

        private int UserInsertDB(RegisterViewModel model)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"INSERT INTO users(username, name, email, password, isverified, usertype, defaultimagename, isactive) 
                                VALUES(@username, @name, @email, @pass, @isverified, @usertype, '', 1)";
            cmd.Parameters.AddWithValue("@username", model.UserName);
            cmd.Parameters.AddWithValue("@name", model.FullName);
            cmd.Parameters.AddWithValue("@email", model.Email);
            cmd.Parameters.AddWithValue("@pass", model.Password);
            cmd.Parameters.AddWithValue("@isverified", false);
            cmd.Parameters.AddWithValue("@usertype", usertype); 

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                if (count > 0)
                {
                    TempData["AlertMessage"] = "<p class='alert alert-success'>Your account was successfully created. Please wait or contact the school administrator for your account to be verified.</p>";
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
        #endregion Register

        #region Classes and Sections
        public ActionResult ManageClasses(int? page)
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            ClassViewModel model = new ClassViewModel();
            int pageSize = 25;
            int pageNumber = (page ?? 1);

            List<ClassModel> list = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString());
            List<TeacherModel> teachers = GetAllTeachers();
            model.Classes = list.ToPagedList(pageNumber, pageSize);
            model.TeacherList = teachers;

            return View(model);
        }  
        [HttpPost] 
        public JsonResult AddClass(ClassModel model, string action)
        { 
            ResultModel resultModel = new ResultModel();
            try
            {
                // Check if sections are provided
                if (model.Sections == null || !model.Sections.Any())
                { 
                    return Json(new ResultModel() { success = false, message = "At least one section is required." });
                }

                resultModel = SaveClassDB(model, action); 

                if(resultModel != null && resultModel.id > 0)
                {
                    foreach (var sect in model.Sections)
                    {
                        ResultModel sectionResultModel = new ResultModel();
                        sectionResultModel = AddSectionByClassDB(sect.Name, resultModel.id, sect.TeacherID); 
                    } 
                }
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message; 
            }
            return Json(resultModel);
        }
         
        [HttpPost]
        public JsonResult SaveClass(int classid, string edit_text, string action)
        {
            ResultModel resultModel = new ResultModel();
            try
            {
                if(action == "update")
                {
                    ClassModel model = new ClassModel();
                    model.ID = classid;
                    model.Name = edit_text;
                    resultModel = SaveClassDB(model, action);
                }
                else
                {
                    ClassModel model = new ClassModel();
                    model.ID = classid; 
                    resultModel = SaveClassDB(model, action);
                }
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
                return Json(resultModel);
            }
            return Json(resultModel);
        }

        public ActionResult EditClass(int? id)
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }
              
            var classModel = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().FirstOrDefault(c => c.ID == id);
            if (classModel == null)
            {
                return HttpNotFound();
            }

            var sectionsByClassID = GetAllSectionsByClassIDDB(classModel.ID).AsEnumerable();
            var teachers = GetAllTeachers().AsEnumerable();
            // Prepare the view model
            var viewModel = new ClassEditViewModel
            {
                ID = classModel.ID,
                Name = classModel.Name,
                Sections = sectionsByClassID.Select(s => new SectionEditViewModel
                {
                    ID = s.ID,
                    Name = s.Name,
                    TeacherID = s.TeacherID
                }).ToList(),
                TeacherList = teachers.Select(t => new TeacherEditViewModel
                {
                    ID = t.ID,
                    Name = t.Name
                }).ToList()
            };

            return View(viewModel);
        }
        [HttpPost]
        public ActionResult EditClass(ClassEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                var classToUpdate = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().FirstOrDefault(c => c.ID == model.ID);
                if (classToUpdate == null)
                {
                    return HttpNotFound();
                }
                 
                var sectionsByClassID = GetAllSectionsByClassIDDB(model.ID).AsEnumerable();
                classToUpdate.Sections = sectionsByClassID.ToList();

                // Remove existing sections if not in the updated model
                var existingSections = classToUpdate.Sections.ToList();
                foreach (var section in existingSections)
                {
                    if (!model.Sections.Any(s => s.ID == section.ID))
                    {
                        SectionModel sectionDeleteModel = new SectionModel();
                        sectionDeleteModel.ID = section.ID;
                        SaveSectionDB(sectionDeleteModel, "delete");
                    }
                }

                // Update or add sections
                foreach (var sectionModel in model.Sections)
                {
                    var sectionToUpdate = classToUpdate.Sections.FirstOrDefault(s => s.ID == sectionModel.ID);
                    if (sectionToUpdate != null)
                    {
                        // Update section name and teachers 
                        SectionModel sectionUpdateModel = new SectionModel();
                        sectionUpdateModel.ID = sectionModel.ID;
                        sectionUpdateModel.Name = sectionModel.Name;
                        sectionUpdateModel.TeacherID = sectionToUpdate.TeacherID;
                        SaveSectionDB(sectionUpdateModel, "update");
                    }
                    else
                    {
                        // Add new section
                        SectionModel sectionAddModel = new SectionModel();
                        sectionAddModel.ID = sectionModel.ID;
                        sectionAddModel.Name = sectionModel.Name;
                        sectionAddModel.ClassID = model.ID;
                        sectionAddModel.TeacherID = sectionModel.TeacherID;
                        SaveSectionDB(sectionAddModel, "add");
                    }
                }
                 
                return RedirectToAction("ManageClasses"); // Redirect after successful update
            }

            // If model state is invalid, reload the form
            model.TeacherList = GetAllTeachers().Select(t => new TeacherEditViewModel
            {
                ID = t.ID,
                Name = t.Name
            }).ToList();

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



        private List<ClassModel> GetAllClasses(int userid, string usertype)
        {
            List<ClassModel> list = new List<ClassModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (usertype == "admin")
            {
                cmd.CommandText = "select * from classes";
            }
            else if (usertype == "teacher")
            {
                cmd.CommandText = $"select * from classes where id in (select classid from sections where teacherid = {userid})";
            }

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    ClassModel model = new ClassModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    model.Sections = GetAllSectionsByClassIDDB(model.ID);
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
        private ResultModel SaveClassDB(ClassModel model, string action)
        {
            int count = 0, lastInsertedId = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (action == "update")
            {
                cmd.CommandText = @"UPDATE `classes` SET `name` = @name WHERE `id` = @id";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@name", model.Name); 
            }
            else if (action == "delete")
            {
                resultActionMessage = action;
                cmd.CommandText = "DELETE FROM `classes` WHERE `id` = @id";
                cmd.Parameters.AddWithValue("@id", model.ID);
            }
            else
            {
                cmd.CommandText = @"INSERT INTO `classes`(`name`) VALUES (@name);SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@name", model.Name);
            }

            try
            {
                con.Open();

                if (action == "add")
                {
                    // Execute the query and get the last inserted ID
                    lastInsertedId = Convert.ToInt32(cmd.ExecuteScalar());
                    count = lastInsertedId;
                    model.ID = lastInsertedId;
                }
                else
                {
                    count = cmd.ExecuteNonQuery();
                }
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Class successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} class. Please check your data.";
                resultModel.id = model.ID;
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + "<br>Inner Exception: " + ex.InnerException.Message;
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }

        private List<SectionModel> GetAllSectionsDB()
        {
            List<SectionModel> list = new List<SectionModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select * from sections";

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    SectionModel model = new SectionModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    model.ClassID = rd["classid"] != null && rd["classid"].ToString() != "" ? Convert.ToInt32(rd["classid"].ToString()) : 0;
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
            cmd.CommandText = "select * from sections where classid = @classid";
            cmd.Parameters.AddWithValue("@classid", classid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    SectionModel model = new SectionModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
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
        private List<TeacherEditViewModel> GetAllTeachersByClassIDDB(int classid)
        {
            List<TeacherEditViewModel> list = new List<TeacherEditViewModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT sec.classid, c.name as classname, sec.teacherid, u.name as teachername, sec.id as sectionid, sec.name as sectionname
                                FROM `users` u 
                                INNER JOIN `sections` sec on sec.teacherid = u.id
                                INNER JOIN `classes` c on sec.classid = c.id
                                WHERE u.usertype = 'teacher' and sec.classid = @classid
                                ORDER BY u.name";
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
        private ResultModel SaveSectionDB(SectionModel model, string action)
        {
            int count = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (action == "update")
            {
                cmd.CommandText = @"UPDATE `sections` SET `name` = @name, `classid` = @classid, `teacherid` = @teacherid WHERE `id` = @id";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@classid", model.ClassID);
                cmd.Parameters.AddWithValue("@teacherid", model.TeacherID);
            }
            else if (action == "delete")
            {
                resultActionMessage = action;
                cmd.CommandText = "DELETE FROM `sections` WHERE `id` = @id";
                cmd.Parameters.AddWithValue("@id", model.ID);
            }
            else
            {
                cmd.CommandText = @"INSERT INTO `sections`(`name`, `classid`, `teacherid`) VALUES (@name,@classid,@teacherid);";
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@classid", model.ClassID);
                cmd.Parameters.AddWithValue("@teacherid", model.TeacherID);
            }

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Section successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} section. Please check your data."; 
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + "<br>Inner Exception: " + ex.InnerException.Message;
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }

        private ResultModel AddSectionByClassDB(string sectionname, int classid, int teacherid)
        {
            int count = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"INSERT INTO `sections`(`name`, `classid`, `teacherid`) VALUES (@name,@classid,@teacherid);";
            cmd.Parameters.AddWithValue("@name", sectionname);
            cmd.Parameters.AddWithValue("@classid", classid);
            cmd.Parameters.AddWithValue("@teacherid", teacherid);

            try
            {
                con.Open(); 
                count = cmd.ExecuteNonQuery(); 
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Section successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} section. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + "<br>Inner Exception: " + ex.InnerException.Message;
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }
         
        #endregion Classes and Sections



        #region Students
        public ActionResult ManageStudents(string keywords, int? status, int? classid, int? page)
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }  
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            StudentViewModel model = new StudentViewModel();
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            int pageSize = 25;
            int pageNumber = (page ?? 1);
             
            List<StudentModel> list = GetAllStudents((int)Session["UserID"], Session["UserType"].ToString());

            if (list != null && list.Count > 0)
            {
                if (!string.IsNullOrEmpty(keywords))
                {
                    list = list.Where(l => l.Name.Contains(keywords) || l.Email.Contains(keywords) || l.UserName.Contains(keywords)).ToList();
                }

                if(status.HasValue)
                {
                    var stats = (status.Value == 1);
                    list = list.Where(l => l.IsVerified == stats).ToList();
                }

                if(classid.HasValue)
                {
                    list = list.Where(l => l.ClassID == classid.Value).ToList();
                }
            }

            model.Students = list.ToPagedList(pageNumber, pageSize);

            return View(model);
        }

        public ActionResult AddStudent()
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            StudentModel model = new StudentModel();
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddStudent(StudentModel model)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            ResultModel resultModel = new ResultModel();
            try
            {
                if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                {
                    string filetype = System.IO.Path.GetExtension(model.ImageFile.FileName);
                    if (filetype.Contains(".jpg") || filetype.Contains(".jpeg") || filetype.Contains(".png"))
                    {
                        model.DefaultImageName = model.UserName + filetype; 
                    }
                }

                model.UpdatedBy = (int)Session["UserID"];
                resultModel = SaveStudentDB(model, "add");

                if (resultModel.success)
                {
                    if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                    {
                        string path = Server.MapPath("~/Images/Users/" + model.DefaultImageName);
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);

                        model.ImageFile.SaveAs(path);
                    }

                    TempData["AlertMessage"] = $"<p class='alert alert-{(resultModel.success ? "success" : "danger")}'>" + resultModel.message + "</p>";
                    return RedirectToAction("ManageStudents");
                }
                else
                {
                    TempData["AlertMessage"] = $"<p class='alert alert-{(resultModel.success ? "success" : "danger")}'>" + resultModel.message + "</p>";
                }
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }

            StudentModel returnModel = new StudentModel();
            returnModel.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() }); 
            return View(returnModel);
        }

        public ActionResult EditStudent(int? id)
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            { 
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            StudentModel model = new StudentModel();
            model = GetStudentById(id.Value);
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() }); 
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditStudent(StudentModel model)
        {
            ResultModel resultModel = new ResultModel(); 
            try
            {
                if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                {
                    string filetype = System.IO.Path.GetExtension(model.ImageFile.FileName);
                    if (filetype.Contains(".jpg") || filetype.Contains(".jpeg") || filetype.Contains(".png"))
                    {
                        model.DefaultImageName = model.UserName + filetype;
                    }
                }
                 
                model.UpdatedBy = (int)Session["UserID"];
                resultModel = SaveStudentDB(model, "update");

                if (resultModel.success)
                {
                    if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                    {
                        string path = Server.MapPath("~/Images/Users/" + model.DefaultImageName);
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);

                        model.ImageFile.SaveAs(path);
                    }

                    TempData["AlertMessage"] = $"<p class='alert alert-{(resultModel.success ? "success" : "danger")}'>" + resultModel.message + "</p>";
                    return RedirectToAction("ManageStudents");
                }
                else
                { 
                    model = GetStudentById(model.ID);
                    TempData["AlertMessage"] = $"<p class='alert alert-{(resultModel.success ? "success" : "danger")}'>" + resultModel.message + "</p>";
                }
            }
            catch (Exception ex)
            {
                model = GetStudentById(model.ID);
                resultModel.success = false;
                resultModel.message = ex.Message;
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + resultModel.message + "</p>";
            }

            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }

        [HttpPost]
        public JsonResult DeleteStudent(StudentModel model, string action)
        {
            ResultModel resultModel = new ResultModel();
            resultModel = SaveStudentDB(model, action);
            return Json(resultModel);
        }

        [HttpPost]
        public JsonResult VerifyStudent(int studentid, bool isverify)
        {
            ResultModel resultModel = new ResultModel();
            resultModel = VerifyStudentDB(studentid, isverify);
            return Json(resultModel);
        }

        [HttpPost]
        public JsonResult UnLockUser(int userid, bool islock)
        {
            ResultModel resultModel = new ResultModel();
            resultModel = UnLockUserDB(userid, islock);
            return Json(resultModel);
        }



        [HttpPost]
        public FileResult ExportStudentList()
        { 
            DataTable dt = new DataTable("Students");
            dt.Columns.AddRange(new DataColumn[3] { new DataColumn("User Name"),
                                            new DataColumn("Full Name"),
                                            new DataColumn("Email")});

            List<StudentModel> list = GetAllStudents((int)Session["UserID"], Session["UserType"].ToString());

            foreach (var stud in list)
            {
                dt.Rows.Add(stud.UserName, stud.Name, stud.Email);
            }

            using (XLWorkbook wb = new XLWorkbook())
            {
                wb.Worksheets.Add(dt);
                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Students.xlsx");
                }
            }
        }


        public ActionResult TrackStudentProgress(int? id)
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            StudentTrackingViewModel model = new StudentTrackingViewModel();
            model.StudentTrackingList = GetStudentProgress(id.Value);
            model.StudentAssignedStoriesAndProgressList = GetAllStudentAssignedStoriesProgress(id.Value);


            return View(model);
        }

        private List<StudentModel> GetAllStudents(int userid, string usertype)
        {
            List<StudentModel> list = new List<StudentModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (usertype == "admin")
            {
                cmd.CommandText = @"select u.*, sc.classid, c.name as classname, sc.sectionid, sec.name as sectionname 
                                    from users u
                                    left join studentclasses sc on sc.studentid = u.id
                                    left join classes c on c.id = sc.classid
                                    left join sections sec on sec.id = sc.sectionid
                                    where u.usertype = 'student'";
            }
            else
            {
                cmd.CommandText = @"select u.*, sc.classid, c.name as classname, sc.sectionid, sec.name as sectionname 
                                    from users u
                                    left join studentclasses sc on sc.studentid = u.id
                                    left join classes c on c.id = sc.classid
                                    left join sections sec on sec.id = sc.sectionid
                                    where u.usertype = 'student' and sec.teacherid = @teacherid";
                cmd.Parameters.AddWithValue("@teacherid", userid);
            }

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StudentModel model = new StudentModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.UserName = rd["username"] != null ? rd["username"].ToString() : "";
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    model.Email = rd["email"] != null ? rd["email"].ToString() : "";
                    model.IsVerified = rd["isverified"] != null && rd["isverified"].ToString() != "" ? Convert.ToBoolean(rd["isverified"].ToString()) : false;
                    model.DefaultImageName = rd["defaultimagename"] != null ? rd["defaultimagename"].ToString() : "";
                    model.ClassID = rd["classid"] != null && rd["classid"].ToString() != "" ? Convert.ToInt32(rd["classid"].ToString()) : 0;
                    model.ClassName = rd["classname"] != null ? rd["classname"].ToString() : "";
                    model.SectionID = rd["sectionid"] != null && rd["sectionid"].ToString() != "" ? Convert.ToInt32(rd["sectionid"].ToString()) : 0;
                    model.SectionName = rd["sectionname"] != null ? rd["sectionname"].ToString() : "";
                    model.IsActive = rd["isactive"] != null && rd["isactive"].ToString() != "" ? Convert.ToBoolean(rd["isactive"].ToString()) : false;
                    model.IsLock = rd["islock"] != null && rd["islock"].ToString() != "" ? Convert.ToBoolean(rd["islock"].ToString()) : false;
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
        private StudentModel GetStudentById(int id)
        {
            StudentModel model = new StudentModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select u.*, sc.classid, sc.sectionid from users u left join studentclasses sc on sc.studentid = u.id where u.id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                { 
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.UserName = rd["username"] != null ? rd["username"].ToString() : "";
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    model.Email = rd["email"] != null ? rd["email"].ToString() : "";
                    model.IsVerified = rd["isverified"] != null && rd["isverified"].ToString() != "" ? Convert.ToBoolean(rd["isverified"].ToString()) : false;
                    model.DefaultImageName = rd["defaultimagename"] != null && rd["defaultimagename"].ToString() != "" ? rd["defaultimagename"].ToString() : "defaultstudentimage.png";
                    model.Password = rd["password"] != null ? rd["password"].ToString() : ""; 
                    model.ClassID = rd["classid"] != null && rd["classid"].ToString() != "" ? Convert.ToInt32(rd["classid"].ToString()) : 0;
                    model.SectionID = rd["sectionid"] != null && rd["sectionid"].ToString() != "" ? Convert.ToInt32(rd["sectionid"].ToString()) : 0;
                    model.IsActive = rd["isactive"] != null && rd["isactive"].ToString() != "" ? Convert.ToBoolean(rd["isactive"].ToString()) : false;
                    model.IsVerified = rd["isverified"] != null && rd["isverified"].ToString() != "" ? Convert.ToBoolean(rd["isverified"].ToString()) : false;
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return model;
        }
        private ResultModel SaveStudentDB(StudentModel model, string action)
        {
            int count = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (action == "update")
            {
                cmd.CommandText = @"UPDATE `users` SET `username`=@username,`name`=@name,`email`=@email,`password`=(case when @password is null or @password = '' then password else @password end)
                                        , `defaultimagename`=(case when @defaultimagename = '' then defaultimagename else @defaultimagename end), `isactive` = @isactive, `isverified` = @isverified 
                                    WHERE `id` = @id;
 
                                    IF (SELECT COUNT(*) FROM `studentclasses` WHERE `studentid` = @id) > 0 THEN 
                                        UPDATE `studentclasses` 
                                        SET `classid` = @classid, `sectionid` = @sectionid
                                        WHERE `studentid` = @id;
                                    ELSE 
                                        INSERT INTO `studentclasses` (`studentid`, `classid`, `sectionid`)
                                        VALUES (@id, @classid, @sectionid);
                                    END IF;";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@username", model.UserName);
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@email", model.Email);
                cmd.Parameters.AddWithValue("@password", model.Password);
                cmd.Parameters.AddWithValue("@defaultimagename", model.DefaultImageName);
                cmd.Parameters.AddWithValue("@classid", model.ClassID);
                cmd.Parameters.AddWithValue("@sectionid", model.SectionID);
                cmd.Parameters.AddWithValue("@isactive", model.IsActive);
                cmd.Parameters.AddWithValue("@isverified", model.IsVerified);  
            }
            else if (action == "delete")
            {
                resultActionMessage = action;
                cmd.CommandText = @"DELETE FROM `users` WHERE `id` = @id;
                                    DELETE FROM studentclasses where studentid = @id;";
                cmd.Parameters.AddWithValue("@id", model.ID);
            }
            else
            {
                cmd.CommandText = @"INSERT INTO `users`(`username`, `name`, `email`, `password`, `isverified`, `usertype`, `defaultimagename`, `isactive`) 
                                    VALUES (@username,@name,@email,@password,1,@usertype,@defaultimagename,1);
                                    
                                    IF @usertype = 'student' THEN   
                                        INSERT INTO `studentclasses`(`studentid`, `classid`, `sectionid`)
                                        VALUES (LAST_INSERT_ID(), @classid, @sectionid);
                                    END IF;
                                    ";
                cmd.Parameters.AddWithValue("@username", model.UserName);
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@email", model.Email);
                cmd.Parameters.AddWithValue("@password", model.Password);
                cmd.Parameters.AddWithValue("@usertype", "student");
                cmd.Parameters.AddWithValue("@defaultimagename", model.DefaultImageName);
                cmd.Parameters.AddWithValue("@teacherid", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@classid", model.ClassID);
                cmd.Parameters.AddWithValue("@sectionid", model.SectionID);
            }

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Student successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} student. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + ( ex.InnerException != null ? "<br>Inner Exception: " + ex.InnerException.Message : "");
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }

        private List<StudentTrackingModel> GetStudentProgress(int studentid)
        {
            List<StudentTrackingModel> list = new List<StudentTrackingModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT stud.StoryTitle, stud.StudentName, stud.ResultPercentage 
                                FROM (
                                    SELECT s.title as StoryTitle, u.name as StudentName, g.totalquestions, g.grade 
                                    	, CAST(((CASE WHEN g.totalquestions = 0 THEN 0 ELSE ((CAST(g.grade AS DECIMAL(10,2)))/(CAST((g.totalquestions * 10) AS DECIMAL(10,2)))) END) * 100.0) AS DECIMAL(10,2)) as ResultPercentage 
                                    FROM (
                                    	select *, ROW_NUMBER() over(partition by sg.storyid order by attempt desc) as row_number from `studentgrades` sg 
                                    ) g
                                    left join stories s on s.id = g.storyid
                                    left join users u on u.id = g.studentid
                                    WHERE g.studentid = @studentid and g.row_number = 1
                                ) stud;";
            cmd.Parameters.AddWithValue("@studentid", studentid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StudentTrackingModel model = new StudentTrackingModel(); 
                    model.StoryTitle = rd["StoryTitle"] != null ? rd["StoryTitle"].ToString() : "";
                    model.StudentName = rd["StudentName"] != null ? rd["StudentName"].ToString() : "";
                    model.ResultPercentage = rd["ResultPercentage"] != null && rd["ResultPercentage"].ToString() != "" ? Convert.ToDecimal(rd["ResultPercentage"].ToString()) : 0; 
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

        private List<StudentTrackingModel> GetAllStudentAssignedStoriesProgress(int studentid)
        {
            List<StudentTrackingModel> list = new List<StudentTrackingModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT distinct stud.StoryTitle, stud.grade, stud.totalscore, CAST(((CASE WHEN stud.totalquestions = 0 THEN 0 ELSE ((CAST(stud.grade AS DECIMAL(10,2)))/(CAST((stud.totalquestions * 10) AS DECIMAL(10,2)))) END) * 100.0) AS DECIMAL(10,2)) as ResultPercentage, stud.isdeleted  
                                FROM (
                                    SELECT * FROM (
                                        SELECT s.title as StoryTitle, g.totalquestions, g.grade, (g.totalquestions * 10) as totalscore, s.isdeleted
                                        FROM stories s   
                                        left join (
                                            select *, ROW_NUMBER() over(partition by sg.storyid order by attempt desc) as row_number from `studentgrades` sg 
                                        ) g on s.id = g.storyid
                                        left join users u on u.id = g.studentid
                                        WHERE g.studentid = @studentid and g.row_number = 1
                                    ) sg
                                    
                                    UNION ALL
                                    
                                    SELECT * FROM (
                                    	SELECT s.title as StoryTitle, case when sq.totalquestions is null then 0 else sq.totalquestions end as totalquestions
                                            , case when g.grade is null then 0 else g.grade end as grade, case when sq.totalquestions is null then 0 else (sq.totalquestions * 10) end as totalscore, s.isdeleted 
                                        FROM `storystudentassignments` ssa
                                        inner join stories s on s.id = ssa.storyid
                                        left join (
                                            select *, ROW_NUMBER() over(partition by sg.storyid order by attempt desc) as row_number from `studentgrades` sg 
                                        ) g on s.id = g.storyid and ssa.studentid = g.studentid
                                        left join users u on u.id = g.studentid 
                                        left join (
                                        	select storyid, COUNT(question) as totalquestions FROM storyquestions
                                            group by storyid
                                        ) sq on sq.storyid = s.id
                                        WHERE ssa.studentid = @studentid and (g.row_number is null or g.row_number = 1)
                                    ) sa
                                ) stud order by stud.StoryTitle;";
            cmd.Parameters.AddWithValue("@studentid", studentid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StudentTrackingModel model = new StudentTrackingModel();
                    model.StoryTitle = rd["StoryTitle"] != null ? rd["StoryTitle"].ToString() : ""; 
                    model.Grade = rd["grade"] != null && rd["grade"].ToString() != "" ? Convert.ToInt32(rd["grade"].ToString()) : 0;
                    model.TotalScore = rd["totalscore"] != null && rd["totalscore"].ToString() != "" ? Convert.ToInt32(rd["totalscore"].ToString()) : 0;
                    model.ResultPercentage = rd["ResultPercentage"] != null && rd["ResultPercentage"].ToString() != "" ? Convert.ToDecimal(rd["ResultPercentage"].ToString()) : 0; 
                    model.IsStoryDeleted = rd["isdeleted"] != null && rd["isdeleted"].ToString() != "" ? Convert.ToInt32(rd["isdeleted"].ToString()) : 0;


                    if (model.IsStoryDeleted > 0)
                    {
                        model.StoryTitle = model.StoryTitle + " <span class='badge rounded-pill bg-danger'>deleted</span>";
                    }

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


        private ResultModel VerifyStudentDB(int studentid, bool isverify)
        {
            int count = 0; 
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"UPDATE `users` SET `isverified` = @isverified WHERE `id` = @id; select COUNT(id) as cntNotVerifyStudents from users where usertype = 'student' and isverified != 1;";
            cmd.Parameters.AddWithValue("@id", studentid);
            cmd.Parameters.AddWithValue("@isverified", isverify);

            try
            {
                con.Open();
                //count = cmd.ExecuteNonQuery();
                Session["CountNotVeriedStudents"] = Convert.ToInt32(cmd.ExecuteScalar());
                count++;
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Student successfully verified." : $"Failed to verify student. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + (ex.InnerException != null ? "<br>Inner Exception: " + ex.InnerException.Message : "");
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }

        private ResultModel UnLockUserDB(int userid, bool islock)
        {
            int count = 0;
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"UPDATE `users` SET `islock` = @islock, datelocked = null, login_attempt = 0 WHERE `id` = @id;";
            cmd.Parameters.AddWithValue("@id", userid);
            cmd.Parameters.AddWithValue("@islock", islock);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"User successfully unlock." : $"Failed to unlock user. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + (ex.InnerException != null ? "<br>Inner Exception: " + ex.InnerException.Message : "");
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }
        #endregion Students

        #region Teacher
        public ActionResult ManageTeachers(string keywords, int? status, int? classid, int? page)
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            TeacherViewModel model = new TeacherViewModel();
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            int pageSize = 25;
            int pageNumber = (page ?? 1);

            List<TeacherModel> list = GetAllTeachers();

            if (list != null && list.Count > 0)
            {
                if (!string.IsNullOrEmpty(keywords))
                {
                    list = list.Where(l => l.Name.Contains(keywords) || l.Email.Contains(keywords) || l.UserName.Contains(keywords)).ToList();
                }

                if (status.HasValue)
                {
                    var stats = (status.Value == 1);
                    list = list.Where(l => l.IsVerified == stats).ToList();
                }

                if (classid.HasValue)
                {
                    list = list.Where(l => l.ClassID == classid.Value).ToList();
                }
            }

            model.Teachers = list.ToPagedList(pageNumber, pageSize);

            return View(model);
        }

        public ActionResult AddTeacher()
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            TeacherModel model = new TeacherModel();
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddTeacher(TeacherModel model, FormCollection form)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }
             
            ResultModel resultModel = new ResultModel();
            try
            {
                if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                {
                    string filetype = System.IO.Path.GetExtension(model.ImageFile.FileName);
                    if (filetype.Contains(".jpg") || filetype.Contains(".jpeg") || filetype.Contains(".png"))
                    {
                        model.DefaultImageName = model.UserName + filetype;
                    }
                }

                model.UpdatedBy = (int)Session["UserID"];
                resultModel = SaveTeacherDB(model, "add");

                if (resultModel.success)
                {
                    if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                    {
                        string path = Server.MapPath("~/Images/Users/" + model.DefaultImageName);
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);

                        model.ImageFile.SaveAs(path);
                    }

                    if(model.ClassIDs != null && model.ClassIDs.Count > 0)
                    {
                        foreach (var classId in model.ClassIDs)
                        {
                            var sectionIdPerClassSelection = form[$"ClassSectionSelections[{classId}].SectionID"];
                            if (!string.IsNullOrEmpty(sectionIdPerClassSelection))
                            {
                                var sectionid = int.Parse(sectionIdPerClassSelection);
                                var teacherSectionResultModel = SaveTeacherSectionDB(sectionid, classId, resultModel.id);

                                if(!teacherSectionResultModel.success)
                                {
                                    TempData["AlertMessage"] += $"<p class='alert alert-{(teacherSectionResultModel.success ? "success" : "danger")}'>" + teacherSectionResultModel.message + "</p>";
                                }
                            }
                        }
                    }

                    TempData["AlertMessage"] += $"<p class='alert alert-{(resultModel.success ? "success" : "danger")}'>" + resultModel.message + "</p>";
                    return RedirectToAction("ManageTeachers");
                }
                else
                {
                    TempData["AlertMessage"] = $"<p class='alert alert-{(resultModel.success ? "success" : "danger")}'>" + resultModel.message + "</p>";
                }
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }

            TeacherModel returnModel = new TeacherModel();
            returnModel.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(returnModel);
        }

        public ActionResult EditTeacher(int? id)
        {
            if (Session["UserName"] != null)
            {
                if (Session["UserType"].ToString() == "student")
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            TeacherModel model = new TeacherModel();
            model = GetTeacherByIDDB(id.Value);
            model.ClassIDs = GetSectionsByTeacherDB(id.Value).AsEnumerable().Select(s => s.ClassID).ToList();
            model.ClassSectionSelections = GetSectionsByTeacherDB(id.Value);
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTeacher(TeacherModel model, FormCollection form)
        {
            ResultModel resultModel = new ResultModel();
            try
            {
                if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                {
                    string filetype = System.IO.Path.GetExtension(model.ImageFile.FileName);
                    if (filetype.Contains(".jpg") || filetype.Contains(".jpeg") || filetype.Contains(".png"))
                    {
                        model.DefaultImageName = model.UserName + filetype;
                    }
                }

                model.UpdatedBy = (int)Session["UserID"];
                resultModel = SaveTeacherDB(model, "update");

                if (resultModel.success)
                {
                    if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                    {
                        string path = Server.MapPath("~/Images/Users/" + model.DefaultImageName);
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);

                        model.ImageFile.SaveAs(path);
                    }

                    if (model.ClassIDs != null && model.ClassIDs.Count > 0)
                    {
                        List<int> sectionsUnderTeacher = new List<int>();
                        foreach (var classId in model.ClassIDs)
                        {
                            var sectionIdPerClassSelection = form[$"ClassSectionSelections[{classId}].SectionID"];
                            if (!string.IsNullOrEmpty(sectionIdPerClassSelection))
                            {
                                var sectionid = int.Parse(sectionIdPerClassSelection);
                                var teacherSectionResultModel = SaveTeacherSectionDB(sectionid, classId, resultModel.id);
                                sectionsUnderTeacher.Add(sectionid);

                                if (!teacherSectionResultModel.success)
                                {
                                    TempData["AlertMessage"] += $"<p class='alert alert-{(teacherSectionResultModel.success ? "success" : "danger")}'>" + teacherSectionResultModel.message + "</p>";
                                }
                            }
                        }
                        RemoveTeacherFromSectionDB(string.Join(",", sectionsUnderTeacher), resultModel.id);
                    }

                    TempData["AlertMessage"] = $"<p class='alert alert-{(resultModel.success ? "success" : "danger")}'>" + resultModel.message + "</p>";
                    return RedirectToAction("ManageTeachers");
                }
                else
                {
                    //model = GetStudentById(model.ID);
                    TempData["AlertMessage"] = $"<p class='alert alert-{(resultModel.success ? "success" : "danger")}'>" + resultModel.message + "</p>";
                }
            }
            catch (Exception ex)
            {
                //model = GetStudentById(model.ID);
                resultModel.success = false;
                resultModel.message = ex.Message;
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + resultModel.message + "</p>";
            }

            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }

        [HttpPost]
        public JsonResult DeleteTeacher(TeacherModel model, string action)
        {
            ResultModel resultModel = new ResultModel();
            resultModel = SaveTeacherDB(model, action);
            return Json(resultModel);
        }
        [HttpPost]
        public JsonResult VerifyTeacher(int teacherid, bool isverify)
        {
            ResultModel resultModel = new ResultModel();
            resultModel = VerifyTeacherDB(teacherid, isverify);
            return Json(resultModel);
        }





        private List<TeacherModel> GetAllTeachers()
        {
            List<TeacherModel> list = new List<TeacherModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select * from users u
                                where u.usertype = 'teacher'";

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    TeacherModel model = new TeacherModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.UserName = rd["username"] != null ? rd["username"].ToString() : "";
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    model.Email = rd["email"] != null ? rd["email"].ToString() : "";
                    model.IsVerified = rd["isverified"] != null && rd["isverified"].ToString() != "" ? Convert.ToBoolean(rd["isverified"].ToString()) : false;
                    model.DefaultImageName = rd["defaultimagename"] != null ? rd["defaultimagename"].ToString() : ""; 
                    model.IsActive = rd["isactive"] != null && rd["isactive"].ToString() != "" ? Convert.ToBoolean(rd["isactive"].ToString()) : false;
                    model.IsLock = rd["islock"] != null && rd["islock"].ToString() != "" ? Convert.ToBoolean(rd["islock"].ToString()) : false;
                    model.ClassSectionSelections = GetSectionsByTeacherDB(model.ID);
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
        private TeacherModel GetTeacherByIDDB(int id)
        {
            TeacherModel model = new TeacherModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select u.* from users u where u.id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.UserName = rd["username"] != null ? rd["username"].ToString() : "";
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    model.Email = rd["email"] != null ? rd["email"].ToString() : "";
                    model.IsVerified = rd["isverified"] != null && rd["isverified"].ToString() != "" ? Convert.ToBoolean(rd["isverified"].ToString()) : false;
                    model.DefaultImageName = rd["defaultimagename"] != null && rd["defaultimagename"].ToString() != "" ? rd["defaultimagename"].ToString() : "defaultstudentimage.png";
                    model.Password = rd["password"] != null ? rd["password"].ToString() : ""; 
                    model.IsActive = rd["isactive"] != null && rd["isactive"].ToString() != "" ? Convert.ToBoolean(rd["isactive"].ToString()) : false;
                    model.IsVerified = rd["isverified"] != null && rd["isverified"].ToString() != "" ? Convert.ToBoolean(rd["isverified"].ToString()) : false;
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return model;
        }
        private ResultModel SaveTeacherDB(TeacherModel model, string action)
        {
            int count = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (action == "update")
            {
                cmd.CommandText = @"UPDATE `users` SET `username`=@username,`name`=@name,`email`=@email,`password`=(case when @password is null or @password = '' then password else @password end)
                                        , `defaultimagename`=(case when @defaultimagename = '' then defaultimagename else @defaultimagename end), `isactive` = @isactive, `isverified` = @isverified 
                                    WHERE `id` = @id;";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@username", model.UserName);
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@email", model.Email);
                cmd.Parameters.AddWithValue("@password", model.Password);
                cmd.Parameters.AddWithValue("@defaultimagename", model.DefaultImageName);
                cmd.Parameters.AddWithValue("@classid", model.ClassID); 
                cmd.Parameters.AddWithValue("@isactive", model.IsActive);
                cmd.Parameters.AddWithValue("@isverified", model.IsVerified);
            }
            else if (action == "delete")
            {
                resultActionMessage = action;
                cmd.CommandText = @"DELETE FROM `users` WHERE `id` = @id;
                                    UPDATE `sections` SET `teacherid`=0 WHERE `teacherid`=@id;";
                cmd.Parameters.AddWithValue("@id", model.ID);
            }
            else
            {
                cmd.CommandText = @"INSERT INTO `users`(`username`, `name`, `email`, `password`, `isverified`, `usertype`, `defaultimagename`, `isactive`) 
                                    VALUES (@username,@name,@email,@password,1,@usertype,@defaultimagename,1); SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@username", model.UserName);
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@email", model.Email);
                cmd.Parameters.AddWithValue("@password", model.Password);
                cmd.Parameters.AddWithValue("@usertype", "teacher");
                cmd.Parameters.AddWithValue("@defaultimagename", model.DefaultImageName);
                cmd.Parameters.AddWithValue("@teacherid", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@classid", model.ClassID); 
            }

            try
            {
                con.Open();
                
                if(action == "add")
                {
                    resultModel.id = Convert.ToInt32(cmd.ExecuteScalar());
                    count++;
                }
                else
                {
                    count = cmd.ExecuteNonQuery();
                    resultModel.id = model.ID;
                }


                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Teacher successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} teacher. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + (ex.InnerException != null ? "<br>Inner Exception: " + ex.InnerException.Message : "");
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }
        private ResultModel SaveTeacherSectionDB(int sectionid, int classid, int teacherid)
        {
            int count = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"UPDATE `sections` SET `teacherid`=@teacherid WHERE `id`=@sectionid;";
            cmd.Parameters.AddWithValue("@sectionid", sectionid);
            cmd.Parameters.AddWithValue("@classid", classid);
            cmd.Parameters.AddWithValue("@teacherid", teacherid); 

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Teacher section successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} teacher section. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + (ex.InnerException != null ? "<br>Inner Exception: " + ex.InnerException.Message : "");
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }
        private int RemoveTeacherFromSectionDB(string sections, int teacherid)
        {
            int count = 0; 
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = $"UPDATE `sections` SET `teacherid`=0 WHERE `id` not in ({sections}) and `teacherid`={teacherid}"; 

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery(); 
            } 
            finally
            {
                con.Close();
            }
            return count;
        }
        private ResultModel VerifyTeacherDB(int teacherid, bool isverify)
        {
            int count = 0;
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"UPDATE `users` SET `isverified` = @isverified WHERE `id` = @id; select COUNT(id) as cntNotVerifyTeachers from users where usertype = 'teacher' and isverified != 1;";
            cmd.Parameters.AddWithValue("@id", teacherid);
            cmd.Parameters.AddWithValue("@isverified", isverify);

            try
            {
                con.Open();
                //count = cmd.ExecuteNonQuery();
                Session["CountNotVeriedTeachers"] = Convert.ToInt32(cmd.ExecuteScalar());

                count++;
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Teacher successfully verified." : $"Failed to verify teacher. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message + (ex.InnerException != null ? "<br>Inner Exception: " + ex.InnerException.Message : "");
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }
        private List<ClassSectionSelection> GetSectionsByTeacherDB(int teacherid)
        {
            List<ClassSectionSelection> list = new List<ClassSectionSelection>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select s.*, c.name as classname from sections s inner join classes c on c.id = s.classid where s.teacherid = @teacherid";
            cmd.Parameters.AddWithValue("@teacherid", teacherid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    ClassSectionSelection model = new ClassSectionSelection();
                    model.SectionID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.SectionName = rd["name"] != null ? rd["name"].ToString() : "";
                    model.ClassID = rd["classid"] != null && rd["classid"].ToString() != "" ? Convert.ToInt32(rd["classid"].ToString()) : 0;
                    model.ClassName = rd["classname"] != null ? rd["classname"].ToString() : "";
                    model.Sections = GetAllSectionsByClassIDDB(model.ClassID).AsEnumerable().Select(s => new SectionEditViewModel() 
                    {
                        ID = s.ID,
                        Name = s.Name,
                        TeacherID = teacherid
                    }).ToList();
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
        #endregion Teacher


        #region Stories
        public ActionResult ManageStories(int? page)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            StoryViewModel model = new StoryViewModel();
            int pageSize = 25;
            int pageNumber = (page ?? 1);

            string usertype = Session["UserType"].ToString();
            int teacherid = Convert.ToInt32(Session["UserID"].ToString());
            List<StoryModel> list = GetStoriesByTeacherID(teacherid, usertype);
            model.Stories = list.ToPagedList(pageNumber, pageSize);

            return View(model);
        }

        public ActionResult AddStory()
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            StoryModel model = new StoryModel();
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }

        public JsonResult GetClassSectionsByClassID(int classId)
        {
            List<SectionModel> list = new List<SectionModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select sc.id, sc.name 
                                from sections sc 
                                where sc.classid = @classid";
            cmd.Parameters.AddWithValue("@classid", classId);

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

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetClassStudentsByClassID(int classId)
        { 
            List<StudentModel> list = new List<StudentModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"select u.id, u.name 
                                from users u
                                inner join studentclasses sc on u.id = sc.studentid
                                where u.isactive = 1 and isverified = 1 and sc.classid = @classid";
            cmd.Parameters.AddWithValue("@classid", classId);



            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StudentModel model = new StudentModel();
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

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GetClassStudentsBySectionID(int? classid, string[] sectionIds)
        {
            List<StudentModel> list = new List<StudentModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
             
            if (sectionIds != null && sectionIds.Count() > 0)
            {
                cmd.CommandText = $@"select u.id, u.name 
                                    from users u
                                    inner join studentclasses sc on u.id = sc.studentid
                                    where u.isactive = 1 and isverified = 1 and sc.classid = {(classid.HasValue ? classid.Value : 0)} and sc.sectionid in ({(sectionIds != null && sectionIds.Count() > 0 ? string.Join(",", sectionIds) : "0")})";
            }
            else
            {
                cmd.CommandText = $@"select u.id, u.name 
                                    from users u
                                    inner join studentclasses sc on u.id = sc.studentid
                                    where u.isactive = 1 and isverified = 1 and sc.classid = {(classid.HasValue ? classid.Value : 0)}";
            }


            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StudentModel model = new StudentModel();
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

            return Json(list);
        }

        [HttpPost] 
        public JsonResult SaveStory(StoryModel model, string action)
        { 
            ResultModel resultModel = new ResultModel();
            try
            {
                // Parse JSON string arrays
                var selectedSectionIds = !string.IsNullOrEmpty(Request.Form["SelectedSectionIds"])
                    ? JsonConvert.DeserializeObject<List<int>>(Request.Form["SelectedSectionIds"])
                    : new List<int>();

                var selectedStudentIds = !string.IsNullOrEmpty(Request.Form["SelectedStudentIds"])
                    ? JsonConvert.DeserializeObject<List<int>>(Request.Form["SelectedStudentIds"])
                    : new List<int>();

                model.SelectedSectionIds = selectedSectionIds.ToArray();
                model.SelectedStudentIds = selectedStudentIds.ToArray();

                // Handle the uploaded file
                if (Request.Files.Count > 0)
                {
                    var file = Request.Files["CoverImage"];
                    if (file != null && file.ContentLength > 0)
                    {
                        // Define the path to save the uploaded file
                        var filePath = System.IO.Path.Combine(Server.MapPath("~/Uploads/StoryCovers"), file.FileName);

                        // Save the file
                        file.SaveAs(filePath);

                        // Optionally, store the file path or name in the model or database
                        model.CoverImagePath = "/Uploads/StoryCovers/" + file.FileName;
                        model.CoverImageName = file.FileName;
                    }
                }

                model.UpdatedBy = (int)Session["UserID"];
                model.DateUpdated = DateTime.Now;
                resultModel = SaveStoryDB(model, action);

                if(resultModel.success)
                {
                    if(model.SelectedSectionIds != null && model.SelectedSectionIds.Count() > 0)
                    {
                        DeleteStorySectionAssignmentsByIDDB(resultModel.id);
                        foreach (var sectionid in model.SelectedSectionIds)
                        {
                            if (sectionid > 0)
                                SaveStorySectionAssignmentsDB(resultModel.id, model.ClassID, sectionid, model.UpdatedBy, model.DateUpdated);
                        }
                    }

                    if (model.SelectedStudentIds != null && model.SelectedStudentIds.Count() > 0)
                    {
                        DeleteStoryStudentAssignmentsByIDDB(resultModel.id);
                        foreach (var studentid in model.SelectedStudentIds)
                        {
                            if (studentid > 0)
                                SaveStoryStudentAssignmentsDB(resultModel.id, studentid, model.UpdatedBy, model.DateUpdated);
                        }
                    }

                    if(model.Incomplete)
                    {
                        var getQuestions = GetQuestionsByStoryIDDB(resultModel.id);

                        if (getQuestions != null && getQuestions.Count > 0)
                            action = "update";
                        else
                            action = "add";

                        QuestionModel qModel = new QuestionModel();
                        qModel.StoryID = resultModel.id;
                        qModel.Question = model.Content;
                        qModel.UpdatedBy = model.UpdatedBy; 
                        ResultModel questionResultModel = SaveQuestionDB(qModel, action);
                    }
                }

            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
                return Json(resultModel);
            }
            return Json(resultModel);
        }

        public ActionResult EditStory(int? id)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if(id == null)
            {
                return HttpNotFound();
            }

            StoryModel model = new StoryModel();
            model = GetStoriesByID(id.Value); 
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });


            return View(model);
        }

        [HttpPost]
        public ActionResult UploadStoryImages(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                // Get the file name and save path
                var fileName = System.IO.Path.GetFileName(file.FileName);
                var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(file.FileName);
                var fileExt = System.IO.Path.GetExtension(file.FileName);
                var newFileName = string.Concat(fileNameWithoutExt, DateTime.Now.Ticks.ToString(), fileExt);
                var filePath = System.IO.Path.Combine(Server.MapPath("~/Uploads/Stories"), newFileName);

                // Save the file to the server
                file.SaveAs(filePath);

                // Return the file URL to be inserted into the Summernote editor
                return Json(Url.Content("~/Uploads/Stories/" + newFileName));
            }
            return Json(null);
        }

        public ActionResult UploadStory()
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            StoryFileUploadViewModel model = new StoryFileUploadViewModel();
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult UploadStory(StoryFileUploadViewModel model)
        //{
        //    ResultModel resultModel = new ResultModel();
        //    if (model.ExcelFile != null && model.ExcelFile.ContentLength > 0)
        //    {
        //        var file = model.ExcelFile; 

        //        using (var workbook = new XLWorkbook(file.InputStream))
        //        {
        //            var worksheet = workbook.Worksheets.Worksheet(1); // Get the first worksheet

        //            // Assuming the first row contains column headers
        //            for (int row = 2; row <= worksheet.RowsUsed().Count(); row++)
        //            {
        //                var title = worksheet.Cell(row, 1).GetValue<string>();
        //                var content = worksheet.Cell(row, 2).GetValue<string>();

        //                StoryModel storymodel = new StoryModel();
        //                storymodel.UpdatedBy = (int)Session["UserID"];
        //                storymodel.Title = title;
        //                storymodel.Content = content;
        //                resultModel = SaveStoryDB(storymodel, "add");
        //            }
        //        }

        //        // Process or save the stories as needed
        //        // e.g., save to the database or process the list

        //        return RedirectToAction("ManageStories"); // Redirect to another action or view
        //    }

        //    return View(model);
        //} 

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult UploadStory(StoryFileUploadViewModel model)
        //{
        //    ResultModel resultModel = new ResultModel();
        //    if (model.File != null && model.File.ContentLength > 0)
        //    {
        //        // Ensure the uploaded file is a Word document
        //        var extension = Path.GetExtension(model.File.FileName);
        //        if (extension == ".docx")
        //        {
        //            // Read the Word document
        //            using (var stream = model.File.InputStream)
        //            {
        //                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false))
        //                {
        //                    // Get all paragraphs from the document
        //                    var paragraphs = wordDoc.MainDocumentPart.Document.Body.Elements<Paragraph>()
        //                                            .Select(p => p.InnerText)
        //                                            .Where(text => !string.IsNullOrWhiteSpace(text))
        //                                            .ToList();

        //                    // Set the title as the first paragraph and the story content as the rest
        //                    if (paragraphs.Count > 0)
        //                    { 
        //                        StoryModel storymodel = new StoryModel();
        //                        storymodel.UpdatedBy = (int)Session["UserID"];
        //                        storymodel.Title = paragraphs[0];
        //                        storymodel.Content = string.Join(Environment.NewLine, paragraphs.Skip(1));
        //                        storymodel.ClassID = model.ClassID;
        //                        resultModel = SaveStoryDB(storymodel, "add");
        //                    } 
        //                }
        //            }

        //            return RedirectToAction("ManageStories"); // Redirect to another action or view
        //        }
        //        else
        //        {
        //            ModelState.AddModelError("", "Please upload a valid .docx file.");
        //            model.ClassList = GetAllClasses().AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
        //        }
        //    }
        //    else
        //    {
        //        ModelState.AddModelError("", "No file was uploaded.");
        //    }

        //    return View(model);
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadStory(StoryFileUploadViewModel model)
        {
            ResultModel resultModel = new ResultModel();
            if (model.File != null && model.File.ContentLength > 0)
            {
                // Ensure the uploaded file is a Word document
                var extension = System.IO.Path.GetExtension(model.File.FileName);
                if (extension == ".docx")
                {
                    // Save the uploaded document
                    string filePath = System.IO.Path.Combine(Server.MapPath("~/Uploads"), System.IO.Path.GetFileName(model.File.FileName));
                    model.File.SaveAs(filePath);

                    // Lists to store extracted content
                    List<string> flipbookContent = new List<string>();
                    string title = string.Empty; // Initialize title variable
                     

                    // Open the Word document
                    using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
                    {
                        // Extract body content
                        var body = doc.MainDocumentPart.Document.Body;
                        bool isFirstParagraph = true; // Flag to identify the first paragraph

                        foreach (var element in body.Elements())
                        {
                            if (element is DocumentFormat.OpenXml.Wordprocessing.Paragraph para)
                            {
                                // Only concatenate actual text runs and avoid metadata elements
                                string paragraphText = string.Join("", para.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()
                                                                           .Select(t => t.Text)).Trim();

                                if (!string.IsNullOrEmpty(paragraphText))
                                {
                                    if (isFirstParagraph)
                                    {
                                        // Set the first paragraph as the title
                                        title = paragraphText;
                                        isFirstParagraph = false;
                                    }
                                    else
                                    {
                                        // Wrap the paragraph text in fb-page-content class
                                        flipbookContent.Add($"<p class=\"fb-page-content\">{paragraphText}</p>");
                                    }
                                }
                            }

                            // Extract images from Drawing elements
                            foreach (var drawing in element.Descendants<Drawing>())
                            {
                                var blip = drawing.Descendants<Blip>().FirstOrDefault();
                                if (blip != null)
                                {
                                    try
                                    {
                                        // Get the related image part
                                        var imagePart = (ImagePart)doc.MainDocumentPart.GetPartById(blip.Embed.Value);
                                        string imageFileName = $"{Guid.NewGuid()}.png";
                                        string imagePath = System.IO.Path.Combine(Server.MapPath("~/Uploads/Stories"), imageFileName);

                                        // Save the image
                                        using (var stream = new FileStream(imagePath, FileMode.Create))
                                        {
                                            imagePart.GetStream().CopyTo(stream);
                                        }

                                        // Add image HTML
                                        string imageUrl = Url.Content("~/Uploads/Stories/" + imageFileName);
                                        flipbookContent.Add($"<p class=\"fb-page-content\" style=\"text-align: center;\"><img src='{imageUrl}' alt='Image' /></p>");
                                    }
                                    catch (Exception ex)
                                    {
                                        // Log or handle the error
                                        Console.WriteLine($"Error processing image: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }


                    //Set the title as the first paragraph and the story content as the rest
                    if (flipbookContent.Count > 0)
                    {
                        StoryModel storymodel = new StoryModel();
                        storymodel.UpdatedBy = (int)Session["UserID"];
                        storymodel.DateUpdated = DateTime.Now;
                        storymodel.Title = title; // Pass the title to the view 
                        storymodel.Content = string.Join("", flipbookContent); // Combine paragraphs and images for display
                        storymodel.ClassID = model.ClassID;
                        storymodel.Incomplete = model.Incomplete;
                        storymodel.Subtitle = model.Subtitle;


                        if (model.CoverPage != null && model.CoverPage.ContentLength > 0)
                        {
                            // Define the path to save the uploaded file
                            var filePathCoverPage = System.IO.Path.Combine(Server.MapPath("~/Uploads/StoryCovers"), model.CoverPage.FileName);

                            // Save the file
                            model.CoverPage.SaveAs(filePathCoverPage);

                            // Optionally, store the file path or name in the model or database 
                            storymodel.CoverImageName = model.CoverPage.FileName;
                        }

                        resultModel = SaveStoryDB(storymodel, "add"); 

                        if (resultModel.success)
                        {
                            if (!string.IsNullOrEmpty(model.SelectedSectionIds))
                            {
                                var selectedSectionIds = model.SelectedSectionIds.Split(',').Select(int.Parse).ToArray();
                                DeleteStorySectionAssignmentsByIDDB(resultModel.id);
                                foreach (var sectionid in selectedSectionIds)
                                {
                                    if (sectionid > 0)
                                        SaveStorySectionAssignmentsDB(resultModel.id, model.ClassID, sectionid, storymodel.UpdatedBy, storymodel.DateUpdated);
                                }
                            }

                            if (!string.IsNullOrEmpty(model.SelectedStudentIds))
                            {
                                var selectedStudentIds = model.SelectedStudentIds.Split(',').Select(int.Parse).ToArray();
                                DeleteStoryStudentAssignmentsByIDDB(resultModel.id);
                                foreach (var studentid in selectedStudentIds)
                                {
                                    if (studentid > 0)
                                        SaveStoryStudentAssignmentsDB(resultModel.id, studentid, storymodel.UpdatedBy, storymodel.DateUpdated);
                                }
                            }

                            if (model.Incomplete)
                            {
                                var action = "";
                                var getQuestions = GetQuestionsByStoryIDDB(resultModel.id);

                                if (getQuestions != null && getQuestions.Count > 0)
                                    action = "update";
                                else
                                    action = "add";

                                QuestionModel qModel = new QuestionModel();
                                qModel.StoryID = resultModel.id;
                                qModel.Question = storymodel.Content;
                                qModel.UpdatedBy = storymodel.UpdatedBy;
                                ResultModel questionResultModel = SaveQuestionDB(qModel, action);
                            }

                            return RedirectToAction("ManageStories"); // Redirect to story list
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Content is not compatible.");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Please upload a valid .docx file."); 
                }
            }
            else
            {
                ModelState.AddModelError("", "No file was uploaded.");
            }
            model.ClassList = GetAllClasses((int)Session["UserID"], Session["UserType"].ToString()).AsEnumerable().Select(c => new SelectListItem() { Text = c.Name, Value = c.ID.ToString() });
            return View(model);
        }

        public ActionResult DownloadStoryFormat()
        {
            // Define the folder where your files are stored 
            var filePath = Server.MapPath("~/Uploads/StoryFormat.xlsx");

            // Check if the file exists
            if (!System.IO.File.Exists(filePath))
            {
                return HttpNotFound(); // Return a 404 error if the file is not found
            }

            // Get the file's content type
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var fileBytes = System.IO.File.ReadAllBytes(filePath);

            // Return the file as a download
            return File(fileBytes, contentType, "StoryFormat.xlsx");
        }

        public ActionResult EvaluateStudentSubmissions(int? id)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            List<StudentSubmissionModel> model = GetStudentSubmissions(id.Value);

            return View(model);
        }

        public ActionResult StudentSubmissions(int? id, int? page)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            int pageSize = 25;
            int pageNumber = (page ?? 1);

            List<StoryStudentAssignmentModel> students = GetAllStudentsUnderStoryAssignment(id.Value);

            var storyDetail = GetStoriesByID(id.Value);
            StudentSubmissionViewModel model = new StudentSubmissionViewModel();
            model.StoryID = id.Value;
            model.StoryTitle = storyDetail.Title; 
            model.Students = students.ToPagedList(pageNumber, pageSize);

            return View(model);
        }

        public ActionResult LatestStudentSubmission(int? id, int? stid, int? attempt)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var storyDetail = GetStoriesByID(id.Value);
            LatestStudentSubmissionViewModel model = new LatestStudentSubmissionViewModel();
            model.StoryID = id.Value;
            model.StoryTitle = storyDetail.Title;
            model.StudentID = stid.Value;
            model.Attempt = attempt.Value;
            model.Questions = GetLatestStudentSubmissionDB(id.Value, stid.Value, attempt.Value);

            return View(model);
        }
        public ActionResult StudentSubmissionRandomQuestion(int? id, int? stid)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }


            StudentRandomAnswerModel model = GetStudentRandomAnswerByStory(id.Value, stid.Value);

            return View(model);
        }



        [HttpPost]
        public ActionResult TeacherSubmitStudentGrade(LatestStudentSubmissionViewModel model)
        { 
            ResultModel resultModel = new ResultModel();
            int count = 0;
            if (model != null)
            {
                var checkedby = (int)Session["UserID"];
                var datechecked = DateTime.Now;
                if (model.Questions != null)
                {
                    decimal grade = 0;
                    foreach (var item in model.Questions)
                    {
                        count += UpdateStudentAnswerScores(item.AnswerID, model.StudentID, item.Score, checkedby, datechecked);
                        grade += (decimal)item.Score;
                    }
                    count += InsertStudentGrade(model.StudentID, model.StoryID, model.Attempt, model.TotalQuestions, grade, model.Remarks, checkedby, datechecked);
                }

            }

            resultModel.success = count > 0;
            resultModel.message = count > 0 ? "Submit successfully saved." : "Failed to submit.";

            return Json(resultModel);

        }
        public ActionResult StudentSubmissionHistory(int? id, int? stid, int? page)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            int pageSize = 25;
            int pageNumber = (page ?? 1);

            List<StudentSubmissionHistoryModel> list = GetStudentSubmissionHistoryByStoryDB(id.Value, stid.Value);
            var storyDetail = GetStoriesByID(id.Value);

            StudentSubmissionHistoryViewModel model = new StudentSubmissionHistoryViewModel();
            model.StoryID = id.Value;
            model.StoryTitle = list != null && list.Count > 0 ? list.Select(l => l.StoryTitleAndStudentCombination).FirstOrDefault() : storyDetail.Title;
            model.StudentID = stid.Value;
            model.StudentSubmissionHistories = list.ToPagedList(pageNumber, pageSize);

            return View(model);
        }

        public ActionResult StudentSubmissionHistoryDetail(int? id, int? stid, int? attempt)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            } 

            var studentid = (int)Session["UserID"];
            var list = GetDetailedQuizResultByStoryDB(stid.Value, id.Value, attempt.Value);

            DetailedQuizGradeByStudentViewModel model = new DetailedQuizGradeByStudentViewModel();
            model.QuizGradeDetailedList = list;
            model.StoryID = id.Value; 
            model.StudentID = stid.Value;
            model.Attempt = attempt.Value;

            return View(model);
        }

        [HttpPost]
        public ActionResult AllowToRetake(int storyid, int studentid)
        { 
            ResultModel resultModel = new ResultModel();
            int count = 0;

            count = UpdateStudentGradeToAllowToRetake(storyid, studentid);


            resultModel.success = count > 0;
            resultModel.message = count > 0 ? "Successfully updated to allow students to retake the test." : "Failed to update to allow student to retake the test.";

            return Json(resultModel);

        }






        private List<StoryModel> GetStoriesByTeacherID(int id, string usertype)
        {
            List<StoryModel> list = new List<StoryModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (usertype == "teacher")
            {
                cmd.CommandText = @"select *, c.name as classname from stories s 
                                left join classes c on s.classid = c.id 
                                left join sections sec on sec.classid = c.id
                                where (s.isdeleted is null or s.isdeleted = 0) and sec.teacherid = @teacherid order by s.id desc";
            }
            else
            {
                cmd.CommandText = @"select *, c.name as classname, sections from stories s 
                                left join classes c on s.classid = c.id 
                                left join (
                                	SELECT classid, GROUP_CONCAT(name, ',') as sections FROM `sections` 
									group by classid
                                ) sec on sec.classid = c.id
                                where (s.isdeleted is null or s.isdeleted = 0) order by s.id desc";
            }
            cmd.Parameters.AddWithValue("@teacherid", id);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StoryModel model = new StoryModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Title = rd["title"] != null ? rd["title"].ToString() : "";
                    model.Content = rd["content"] != null ? rd["content"].ToString() : "";
                    model.ClassID = rd["classid"] != null && rd["classid"].ToString() != "" ? Convert.ToInt32(rd["classid"].ToString()) : 0;
                    model.ClassName = rd["classname"] != null ? rd["classname"].ToString() : "";
                    model.CoverImageName = rd["coverimage"] != null ? rd["coverimage"].ToString() : "";
                    model.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    model.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    model.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    model.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    model.DeletedBy = rd["deletedby"] != null && rd["deletedby"].ToString() != "" ? Convert.ToInt32(rd["deletedby"].ToString()) : 0;
                    model.DateDeleted = rd["datedeleted"] != null && rd["datedeleted"].ToString() != "" ? Convert.ToDateTime(rd["datedeleted"].ToString()) : new DateTime(2000, 1, 1);
                    model.IsDeleted = rd["isdeleted"] != null && rd["isdeleted"].ToString() != "" ? Convert.ToBoolean(rd["isdeleted"].ToString()) : false;
                    model.Incomplete = rd["incomplete"] != null && rd["incomplete"].ToString() != "" ? Convert.ToBoolean(rd["incomplete"].ToString()) : false;
                    list.Add(model);
                }
                rd.Close();
            }
            catch (Exception ex)
            {
                TempData["AlertMessage"] = "<p class='alert alert-danger'>" + ex.Message + "</p>";
            }
            finally
            {
                con.Close();
            }
            return list;
        }

        private ResultModel SaveStoryDB(StoryModel model, string action)
        {
            int count = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (action == "update")
            {
                cmd.CommandText = "UPDATE `stories` SET `title`=@title,`content`=@content,`classid`=@classid,`coverimage`=@coverimage,`updatedby`=@updatedby,`dateupdated`=@dateupdated,`incomplete`=@incomplete,`randomquestion`=@randomquestion,`randomansweroption1`=@randomansweroption1,`randomansweroption2`=@randomansweroption2,`randomansweroption3`=@randomansweroption3,`randomansweroption4`=@randomansweroption4,`randomcorrectanswer`=@randomcorrectanswer,`randomquestionhint`=@randomquestionhint,`subtitle`=@subtitle WHERE `id` = @id";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@title", model.Title);
                cmd.Parameters.AddWithValue("@content", model.Content);
                cmd.Parameters.AddWithValue("@classid", model.ClassID);
                cmd.Parameters.AddWithValue("@coverimage", model.CoverImageName);
                cmd.Parameters.AddWithValue("@updatedby", model.UpdatedBy); 
                cmd.Parameters.AddWithValue("@dateupdated", model.DateUpdated);
                cmd.Parameters.AddWithValue("@incomplete", model.Incomplete);
                cmd.Parameters.AddWithValue("@randomquestion", model.RandomQuestion);
                cmd.Parameters.AddWithValue("@randomansweroption1", model.RandomAnswerOption1);
                cmd.Parameters.AddWithValue("@randomansweroption2", model.RandomAnswerOption2);
                cmd.Parameters.AddWithValue("@randomansweroption3", model.RandomAnswerOption3);
                cmd.Parameters.AddWithValue("@randomansweroption4", model.RandomAnswerOption4);
                cmd.Parameters.AddWithValue("@randomcorrectanswer", model.RandomCorrectAnswer);
                cmd.Parameters.AddWithValue("@randomquestionhint", model.RandomQuestionHint);
                cmd.Parameters.AddWithValue("@subtitle", model.Subtitle);
            }
            else if (action == "delete")
            {
                resultActionMessage = action;
                cmd.CommandText = "UPDATE `stories` SET `isdeleted`=1, `deletedby`=@deletedby,`datedeleted`=@datedeleted WHERE `id` = @id";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@deletedby", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@datedeleted", model.DateUpdated);
            }
            else
            {
                cmd.CommandText = "INSERT INTO `stories`(`title`, `content`, `classid`,`coverimage`,`incomplete`,`randomquestion`,`randomansweroption1`,`randomansweroption2`,`randomansweroption3`,`randomansweroption4`,`randomcorrectanswer`,`randomquestionhint`, `subtitle`, `addedby`, `dateadded`, `updatedby`, `dateupdated`, `isdeleted`) VALUES (@title,@content,@classid,@coverimage,@incomplete,@randomquestion,@randomansweroption1,@randomansweroption2,@randomansweroption3,@randomansweroption4,@randomcorrectanswer,@randomquestionhint,@subtitle,@addedby,@dateadded,@updatedby,@dateupdated,0);SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@title", model.Title);
                cmd.Parameters.AddWithValue("@content", model.Content);
                cmd.Parameters.AddWithValue("@classid", model.ClassID);
                cmd.Parameters.AddWithValue("@coverimage", model.CoverImageName);
                cmd.Parameters.AddWithValue("@addedby", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@dateadded", model.DateUpdated);
                cmd.Parameters.AddWithValue("@updatedby", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@dateupdated", model.DateUpdated);
                cmd.Parameters.AddWithValue("@incomplete", model.Incomplete);
                cmd.Parameters.AddWithValue("@randomquestion", model.RandomQuestion != null ? model.RandomQuestion : "");
                cmd.Parameters.AddWithValue("@randomansweroption1", model.RandomAnswerOption1 != null ? model.RandomAnswerOption1 : "");
                cmd.Parameters.AddWithValue("@randomansweroption2", model.RandomAnswerOption2 != null ? model.RandomAnswerOption2 : "");
                cmd.Parameters.AddWithValue("@randomansweroption3", model.RandomAnswerOption3 != null ? model.RandomAnswerOption3 : "");
                cmd.Parameters.AddWithValue("@randomansweroption4", model.RandomAnswerOption4 != null ? model.RandomAnswerOption4 : "");
                cmd.Parameters.AddWithValue("@randomcorrectanswer", model.RandomCorrectAnswer != null ? model.RandomCorrectAnswer : "");
                cmd.Parameters.AddWithValue("@randomquestionhint", model.RandomQuestionHint);
                cmd.Parameters.AddWithValue("@subtitle", model.Subtitle);
            }

            try
            {
                con.Open(); 

                if(action == "add")
                {
                    count++;
                    resultModel.id = Convert.ToInt32(cmd.ExecuteScalar());
                }
                else
                {
                    count = cmd.ExecuteNonQuery();
                    resultModel.id = model.ID;
                }

                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Story successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} story. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message; 
            }
            finally
            { 
                con.Close();
            }
            return resultModel;
        } 

        private StoryModel GetStoriesByID(int id)
        {
            StoryModel model = new StoryModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select * from stories where id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Title = rd["title"] != null ? rd["title"].ToString() : "";
                    model.Content = rd["content"] != null ? rd["content"].ToString() : "";
                    model.ClassID = rd["classid"] != null && rd["classid"].ToString() != "" ? Convert.ToInt32(rd["classid"].ToString()) : 0;
                    model.CoverImageName = rd["coverimage"] != null ? rd["coverimage"].ToString() : "";
                    model.AddedBy = rd["addedby"] != null && rd["addedby"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    model.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    model.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    model.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    model.DeletedBy = rd["deletedby"] != null && rd["deletedby"].ToString() != "" ? Convert.ToInt32(rd["deletedby"].ToString()) : 0;
                    model.DateDeleted = rd["datedeleted"] != null && rd["datedeleted"].ToString() != "" ? Convert.ToDateTime(rd["datedeleted"].ToString()) : new DateTime(2000, 1, 1);
                    model.IsDeleted = rd["isdeleted"] != null && rd["isdeleted"].ToString() != "" ? Convert.ToBoolean(rd["isdeleted"].ToString()) : false;
                    model.Incomplete = rd["incomplete"] != null && rd["incomplete"].ToString() != "" ? Convert.ToBoolean(rd["incomplete"].ToString()) : false;
                    model.RandomQuestion = rd["randomquestion"] != null ? rd["randomquestion"].ToString() : "";
                    model.RandomAnswerOption1 = rd["randomansweroption1"] != null ? rd["randomansweroption1"].ToString() : "";
                    model.RandomAnswerOption2 = rd["randomansweroption2"] != null ? rd["randomansweroption2"].ToString() : "";
                    model.RandomAnswerOption3 = rd["randomansweroption3"] != null ? rd["randomansweroption3"].ToString() : "";
                    model.RandomAnswerOption4 = rd["randomansweroption4"] != null ? rd["randomansweroption4"].ToString() : "";
                    model.RandomCorrectAnswer = rd["randomcorrectanswer"] != null ? rd["randomcorrectanswer"].ToString() : "";
                    model.RandomQuestionHint = rd["randomquestionhint"] != null ? rd["randomquestionhint"].ToString() : "";
                    model.Subtitle = rd["subtitle"] != null ? rd["subtitle"].ToString() : "";
                    model.SelectedSectionIds = GetStorySectionAssignmentsDB(model.ID);
                    model.SelectedStudentIds = GetStoryStudentAssignmentsDB(model.ID);
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

        private int[] GetStorySectionAssignmentsDB(int storyid)
        {
            List<int> list = new List<int>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT * FROM `storysectionassignments` WHERE storyid = @storyid";
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    var sectionid = rd["sectionid"] != null && rd["sectionid"].ToString() != "" ? Convert.ToInt32(rd["sectionid"].ToString()) : 0;
                    list.Add(sectionid);
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return list.ToArray();
        }
        private int[] GetStoryStudentAssignmentsDB(int storyid)
        {
            List<int> list = new List<int>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT * FROM `storystudentassignments` WHERE storyid = @storyid";
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    var studentid = rd["studentid"] != null && rd["studentid"].ToString() != "" ? Convert.ToInt32(rd["studentid"].ToString()) : 0;
                    list.Add(studentid);
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return list.ToArray();
        }

        private List<StudentSubmissionModel> GetStudentSubmissions(int storyid)
        {
            List<StudentSubmissionModel> list = new List<StudentSubmissionModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT stud.StoryTitle, stud.StudentName
	                                , CAST(((CASE WHEN stud.CntTotal = 0 THEN 0 ELSE ((CAST(stud.CntCorrect AS DECIMAL(10,2)))/(CAST(stud.CntTotal AS DECIMAL(10,2)))) END) * 100.0) AS DECIMAL(10,2)) as ResultPercentage 
                                FROM (
                                    SELECT s.title as StoryTitle, u.name as StudentName
    	                                , SUM(case when a.iscorrect is null or a.iscorrect = 0 then 0 else 1 end) as CntCorrect
                                        , (select COUNT(*) from `questions` gg where (gg.isdeleted is null or gg.isdeleted = 0) and gg.courseid = g.storyid) as CntTotal 
                                    FROM `grades` g INNER JOIN `answers` a on g.stud_answerid = a.id 
                                    left join stories s on s.id = g.storyid
                                    left join users u on u.id = g.studentid
                                    WHERE g.storyid = @storyid
                                    GROUP BY s.title, u.name
                                ) stud;";
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StudentSubmissionModel model = new StudentSubmissionModel();
                    model.StoryTitle = rd["StoryTitle"] != null ? rd["StoryTitle"].ToString() : "";
                    model.StudentName = rd["StudentName"] != null ? rd["StudentName"].ToString() : "";
                    model.ResultPercentage = rd["ResultPercentage"] != null && rd["ResultPercentage"].ToString() != "" ? Convert.ToDecimal(rd["ResultPercentage"].ToString()) : 0;
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

        private int SaveStorySectionAssignmentsDB(int storyid, int classid, int sectionid, int assignedby, DateTime dateassigned)
        {
            int count = 0; 
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = "INSERT INTO `storysectionassignments`(`storyid`, `classid`, `sectionid`, `asssignedby`, `dateassigned`) VALUES (@storyid,@classid,@sectionid,@assignedby,@dateassigned);";
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@classid", classid);
            cmd.Parameters.AddWithValue("@sectionid", sectionid);
            cmd.Parameters.AddWithValue("@assignedby", assignedby);
            cmd.Parameters.AddWithValue("@dateassigned", dateassigned);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            } 
            finally
            {
                con.Close();
            }
            return count;
        }
        private int SaveStoryStudentAssignmentsDB(int storyid, int studentid, int assignedby, DateTime dateassigned)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"INSERT INTO `storystudentassignments`(`storyid`, `classid`, `sectionid`, `studentid`, `assignedby`, `dateassigned`) 
                                VALUES (@storyid,(select classid from studentclasses where studentid = @studentid),(select sectionid from studentclasses where studentid = @studentid),@studentid,@assignedby,@dateassigned);";
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@studentid", studentid); 
            cmd.Parameters.AddWithValue("@assignedby", assignedby);
            cmd.Parameters.AddWithValue("@dateassigned", dateassigned);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            }
            finally
            {
                con.Close();
            }
            return count;
        }

        private int DeleteStorySectionAssignmentsByIDDB(int storyid)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"delete from storysectionassignments where storyid = @storyid";
            cmd.Parameters.AddWithValue("@storyid", storyid); 

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            }
            finally
            {
                con.Close();
            }
            return count;
        }

        private int DeleteStoryStudentAssignmentsByIDDB(int storyid)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"delete from storystudentassignments where storyid = @storyid";
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            }
            finally
            {
                con.Close();
            }
            return count;
        }

        private List<StoryStudentAssignmentModel> GetAllStudentsUnderStoryAssignment(int storyid)
        {
            List<StoryStudentAssignmentModel> list = new List<StoryStudentAssignmentModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT sc.studentid, u.name as studentname, sc.classid, sc.sectionid, sec.sectionname, sec.classname, sec.teacherid, sec.teachername, s.id as storyid, s.title as storytitle
                                    , (select MAX(attempt) from studentanswers ssa inner join storyquestions ssq on ssq.id = ssa.questionid where ssa.studentid = sc.studentid and ssa.checkby = 0 and ssq.storyid = s.id) as latestattempt 
                                    , (select MAX(attempt) from studentanswers ssa inner join storyquestions ssq on ssq.id = ssa.questionid where ssa.studentid = sc.studentid and ssq.storyid = s.id) as totalattempts 
                                FROM `users` u
                                INNER JOIN studentclasses sc on u.id = sc.studentid
                                INNER JOIN (
	                                SELECT s.id as sectionid, s.name as sectionname, s.classid, c.name as classname, s.teacherid, u.name as teachername
                                    FROM sections s
                                    INNER JOIN users u on u.id = s.teacherid
                                    INNER JOIN classes c on c.id = s.classid
                                    WHERE u.isactive = 1 and u.isverified = 1
                                ) sec on sec.sectionid = sc.sectionid
                                INNER JOIN stories s on s.classid = sc.classid
                                WHERE u.isactive = 1 and u.isverified = 1 AND s.id = @storyid";
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StoryStudentAssignmentModel model = new StoryStudentAssignmentModel();
                    model.StudentID = rd["studentid"] != null && rd["studentid"].ToString() != "" ? Convert.ToInt32(rd["studentid"].ToString()) : 0;
                    model.StudentName = rd["studentname"] != null ? rd["studentname"].ToString() : "";
                    model.LatestAttempt = rd["latestattempt"] != null && rd["latestattempt"].ToString() != "" ? Convert.ToInt32(rd["latestattempt"].ToString()) : 0;
                    model.TotalAttempts = rd["totalattempts"] != null && rd["totalattempts"].ToString() != "" ? Convert.ToInt32(rd["totalattempts"].ToString()) : 0;
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

        private List<LatestStudentSubmissionQuestionScoreModel> GetLatestStudentSubmissionDB(int storyid, int studentid, int attempt)
        {
            List<LatestStudentSubmissionQuestionScoreModel> list = new List<LatestStudentSubmissionQuestionScoreModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select sa.id as answerid, sa.questionid, sq.question, sa.answer, sa.attempt, sa.dateanswered, sa.studentid, smca.answer_option
                                from storyquestions sq
                                left join studentanswers sa on sa.questionid = sq.id and sa.attempt = @attempt and sa.studentid = @studentid
                                left join storymultiplechoiceanswers smca on smca.questionid = sq.id and smca.iscorrect = 1
                                where sq.storyid = @storyid
                                order by sq.id;";
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@attempt", attempt);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    LatestStudentSubmissionQuestionScoreModel model = new LatestStudentSubmissionQuestionScoreModel();
                    model.AnswerID = rd["answerid"] != null && rd["answerid"].ToString() != "" ? Convert.ToInt32(rd["answerid"].ToString()) : 0;
                    model.QuestionID = rd["questionid"] != null && rd["questionid"].ToString() != "" ? Convert.ToInt32(rd["questionid"].ToString()) : 0;
                    model.Question = rd["question"] != null ? rd["question"].ToString() : "";
                    model.StudentID = rd["studentid"] != null && rd["studentid"].ToString() != "" ? Convert.ToInt32(rd["studentid"].ToString()) : 0;
                    model.Answer = rd["answer"] != null ? rd["answer"].ToString() : "";
                    model.DateAnswered = rd["dateanswered"] != null && rd["dateanswered"].ToString() != "" ? Convert.ToDateTime(rd["dateanswered"].ToString()) : new DateTime(2000, 1, 1);
                    model.MultipleChoiceCorrectAnswer = rd["answer_option"] != null ? rd["answer_option"].ToString() : "";
                    model.MultipleChoiceAnswers = GetAnswersByQuestionID(model.QuestionID);
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

        private int UpdateStudentAnswerScores(int answerid, int studentid, int score, int checkby, DateTime datechecked)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"UPDATE `studentanswers` 
                                SET `score`=@score,`checkby`=@checkby,`datechecked`=@datechecked
                                WHERE `id`= @answerid and `studentid` = @studentid";
            cmd.Parameters.AddWithValue("@answerid", answerid);
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@score", score);
            cmd.Parameters.AddWithValue("@checkby", checkby);
            cmd.Parameters.AddWithValue("@datechecked", datechecked);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            }
            finally
            {
                con.Close();
            }
            return count;
        }

        private int InsertStudentGrade(int studentid, int storyid, int attempt, int totalquestions, decimal grade, string remarks, int checkby, DateTime datechecked)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"IF (SELECT COUNT(*) FROM `studentgrades` WHERE `studentid` = @studentid and `storyid` = @storyid and `attempt` = @attempt) > 0 THEN 
                                    UPDATE `studentgrades` 
                                    SET `totalquestions` = @totalquestions, `grade` = @grade, `remarks` = @remarks, `checkedby` = @checkedby, `datechecked` = @datechecked
                                    WHERE `studentid` = @studentid and `storyid` = @storyid and `attempt` = @attempt;
                                ELSE 
                                    INSERT INTO `studentgrades`(`studentid`, `storyid`, `attempt`, `totalquestions`, `grade`, `remarks`, `checkedby`, `datechecked`) 
                                	VALUES(@studentid, @storyid, @attempt, @totalquestions, @grade, @remarks, @checkedby, @datechecked);
                                END IF;";
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storyid", storyid); 
            cmd.Parameters.AddWithValue("@attempt", attempt);
            cmd.Parameters.AddWithValue("@totalquestions", totalquestions);
            cmd.Parameters.AddWithValue("@grade", grade);
            cmd.Parameters.AddWithValue("@remarks", remarks == null ? "" : remarks);
            cmd.Parameters.AddWithValue("@checkedby", checkby);
            cmd.Parameters.AddWithValue("@datechecked", datechecked);

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            }
            finally
            {
                con.Close();
            }
            return count;
        }

        private List<StudentSubmissionHistoryModel> GetStudentSubmissionHistoryByStoryDB(int storyid, int studentid)
        {
            List<StudentSubmissionHistoryModel> list = new List<StudentSubmissionHistoryModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT sg.storyid, CONCAT(s.title, ' (', ut.name, ')') as storytitleandstudentcombo, sg.attempt, sg.totalquestions, sg.grade, sg.remarks, ut.name as checkedbyname, sg.datechecked
                                FROM stories s  
                                LEFT JOIN (
                                	SELECT DISTINCT (case when sg.totalquestions is null then (select COUNT(*) from storyquestions ssq where ssq.storyid = @storyid) else sg.totalquestions end) totalquestions
                                        , sa.attempt, sg.grade, sg.remarks, sa.studentid, sq.storyid, sg.checkedby, sg.datechecked
                                    FROM `studentanswers` sa
                                    INNER JOIN storyquestions sq on sq.id = sa.questionid
                                    LEFT JOIN studentgrades sg on sg.storyid = sq.storyid and sg.studentid = sa.studentid and sa.attempt = sg.attempt
                                ) sg on s.id = sg.storyid 
                                LEFT JOIN users ut on ut.id = sg.checkedby
                                LEFT JOIN users us on us.id = sg.studentid
                                WHERE sg.storyid = @storyid and sg.studentid = @studentid
                                ORDER BY sg.attempt DESC";
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@studentid", studentid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StudentSubmissionHistoryModel model = new StudentSubmissionHistoryModel();
                    model.StoryTitleAndStudentCombination = rd["storytitleandstudentcombo"] != null ? rd["storytitleandstudentcombo"].ToString() : "";
                    model.StoryID = rd["storyid"] != null && rd["storyid"].ToString() != "" ? Convert.ToInt32(rd["storyid"].ToString()) : 0;
                    model.Attempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0;
                    model.TotalQuestions = rd["totalquestions"] != null && rd["totalquestions"].ToString() != "" ? Convert.ToInt32(rd["totalquestions"].ToString()) : 0;
                    model.Grade = rd["grade"] != null && rd["grade"].ToString() != "" ? Convert.ToInt32(rd["grade"].ToString()) : 0;
                    model.Remarks = rd["remarks"] != null ? rd["remarks"].ToString() : "";
                    model.CheckedByName = rd["checkedbyname"] != null ? rd["checkedbyname"].ToString() : "";
                    model.DateChecked = rd["datechecked"] != null && rd["datechecked"].ToString() != "" ? Convert.ToDateTime(rd["datechecked"].ToString()) : new DateTime(2000, 1, 1);
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
        private List<DetailedQuizGradeByStudentModel> GetDetailedQuizResultByStoryDB(int studentid, int storyid, int attempt)
        {
            List<DetailedQuizGradeByStudentModel> list = new List<DetailedQuizGradeByStudentModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT sa.questionid, sq.question, sa.id as answerid, sa.answer, sa.dateanswered, sa.score, sg.storyid, sg.studentid, sa.attempt, sg.totalquestions, sg.grade, sg.remarks, sg.checkedbyname, sg.datechecked
	                                , (select title from stories where id = @storyid) as storytitle, smca.answer_option  
                                FROM `studentanswers` sa
                                INNER JOIN `storyquestions` sq on sq.id = sa.questionid
                                LEFT JOIN (
	                                    SELECT sg.*, u.name as checkedbyname 
                                        FROM `studentgrades` sg
                                        INNER JOIN users u on u.usertype = 'teacher' and sg.checkedby = u.id 
                                        WHERE sg.storyid = @storyid 
                                    ) sg on sg.attempt = sa.attempt and sg.studentid = sa.studentid
                                LEFT JOIN `storymultiplechoiceanswers` smca on smca.iscorrect = 1 and smca.questionid = sq.id
                                where sa.attempt = @attempt and sa.studentid = @studentid and sq.storyid = @storyid
                                order by sa.questionid";
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@attempt", attempt);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    DetailedQuizGradeByStudentModel model = new DetailedQuizGradeByStudentModel();
                    model.QuestionID = rd["questionid"] != null && rd["questionid"].ToString() != "" ? Convert.ToInt32(rd["questionid"].ToString()) : 0;
                    model.StoryQuestion = rd["question"] != null ? rd["question"].ToString() : "";
                    model.AnswerID = rd["answerid"] != null && rd["answerid"].ToString() != "" ? Convert.ToInt32(rd["answerid"].ToString()) : 0;
                    model.StudentAnswer = rd["answer"] != null ? rd["answer"].ToString() : "";
                    model.Score = rd["score"] != null && rd["score"].ToString() != "" ? Convert.ToInt32(rd["score"].ToString()) : 0;
                    model.DateAnswered = rd["dateanswered"] != null && rd["dateanswered"].ToString() != "" ? Convert.ToDateTime(rd["dateanswered"].ToString()) : new DateTime(2000, 1, 1);
                    model.StudentID = rd["studentid"] != null && rd["studentid"].ToString() != "" ? Convert.ToInt32(rd["studentid"].ToString()) : 0;
                    model.StoryID = rd["storyid"] != null && rd["storyid"].ToString() != "" ? Convert.ToInt32(rd["storyid"].ToString()) : 0;
                    model.StoryTitle = rd["storytitle"] != null ? rd["storytitle"].ToString() : "";
                    model.Attempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0;
                    model.TotalQuestions = rd["totalquestions"] != null && rd["totalquestions"].ToString() != "" ? Convert.ToInt32(rd["totalquestions"].ToString()) : 0;
                    model.Grade = rd["grade"] != null && rd["grade"].ToString() != "" ? Convert.ToInt32(rd["grade"].ToString()) : 0;
                    model.Remarks = rd["remarks"] != null ? rd["remarks"].ToString() : "";
                    model.DateChecked = rd["datechecked"] != null && rd["datechecked"].ToString() != "" ? Convert.ToDateTime(rd["datechecked"].ToString()) : new DateTime(2000, 1, 1);
                    model.CheckedBy = rd["checkedbyname"] != null ? rd["checkedbyname"].ToString() : "";
                    model.MultipleChoiceCorrectAnswer = rd["answer_option"] != null ? rd["answer_option"].ToString() : "";

                    if (model.TotalQuestions > 0)
                    {
                        decimal percentage = (Convert.ToDecimal(model.Grade) / (Convert.ToDecimal(model.TotalQuestions) * 10)) * 100;
                        model.GradePercentage = Math.Round(percentage, 2); // Rounds to 2 decimal places 
                    }
                    else
                    {
                        model.GradePercentage = 0;
                    }

                    model.MultipleChoiceAnswers = GetAnswersByQuestionID(model.QuestionID);
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

        private int UpdateStudentGradeToAllowToRetake(int storyid, int studentid)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"UPDATE `studentstoryprogress` SET `allowretake`= 1, status = 'Reassigned'
                                WHERE `storyid`= @storyid and `studentid` = @studentid";
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@studentid", studentid); 

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
            }
            finally
            {
                con.Close();
            }
            return count;
        }

        private StudentRandomAnswerModel GetStudentRandomAnswerByStory(int storyid, int studentid)
        {
            StudentRandomAnswerModel model = new StudentRandomAnswerModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            cmd.CommandText = @"SELECT sra.*, s.randomquestion, s.title as storytitle FROM `studentrandomanswers` sra
                                inner join stories s on s.id = sra.storyid 
                                WHERE sra.storyid= @storyid and sra.studentid = @studentid";
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@studentid", studentid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                { 
                    model.StudentID = rd["studentid"] != null && rd["studentid"].ToString() != "" ? Convert.ToInt32(rd["studentid"].ToString()) : 0;
                    model.StoryID = rd["storyid"] != null && rd["storyid"].ToString() != "" ? Convert.ToInt32(rd["storyid"].ToString()) : 0;
                    model.StoryTitle = rd["storytitle"] != null ? rd["storytitle"].ToString() : "";
                    model.Question = rd["randomquestion"] != null ? rd["randomquestion"].ToString() : ""; 
                    model.Answer = rd["answer"] != null ? rd["answer"].ToString() : ""; 
                    model.DateAnswered = rd["dateanswered"] != null && rd["dateanswered"].ToString() != "" ? Convert.ToDateTime(rd["dateanswered"].ToString()) : new DateTime(2000, 1, 1);
                    rd.Close();
                } 
            }
            finally
            {
                con.Close();
            }

            return model;
        }
        #endregion Stories


        #region Questions 
        public ActionResult Questions(int? id) //, int? page)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Admin", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            //int pageSize = 25;
            //int pageNumber = (page ?? 1);
            List<QuestionModel> list = GetQuestionsByStoryIDDB(id.Value);
            StoryModel course = GetStoriesByID(id.Value);

            QuestionViewModel model = new QuestionViewModel();
            model.Questions = list; //.ToPagedList(pageNumber, pageSize);
            model.StoryID = id.Value;
            model.StoryTitle = course.Title;  



            return View(model);
        } 


        [HttpPost]
        public JsonResult AddQuestion(QuestionAndAnswerModel model, string action)
        {
            int count = 0;
            ResultQuestionUpdateModel result = new ResultQuestionUpdateModel();
            QuestionModel questionModel = new QuestionModel();
            questionModel.Question = model.Question;
            questionModel.StoryID = model.StoryID;
            questionModel.UpdatedBy = (int)Session["UserID"];
            ResultModel resultQuestionModel = SaveQuestionDB(questionModel, action);

            if(resultQuestionModel != null)
            {
                count++;
                result.success = true;
                result.message = "Successfully saved data.";
                result.question = GetQuestionsByQuestionIDDB(resultQuestionModel.id);
            }

            return Json(result);
        }

        [HttpPost]
        public JsonResult SaveQuestion(QuestionModel model, string action)
        {
            ResultModel resultModel = new ResultModel();
            try
            {
                model.UpdatedBy = (int)Session["UserID"];
                resultModel = SaveQuestionDB(model, action);
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
                return Json(resultModel);
            }
            return Json(resultModel);
        }

        [HttpPost]
        public JsonResult UpdateQuestion(int questionid,  string edit_text)
        {
            ResultModel resultModel = new ResultModel();
            try
            {
                QuestionModel model = new QuestionModel();
                model.ID = questionid;
                model.Question = edit_text;
                model.UpdatedBy = (int)Session["UserID"];
                resultModel = SaveQuestionDB(model, "update");
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
                return Json(resultModel);
            }
            return Json(resultModel);
        }

        [HttpPost]
        public JsonResult SaveQuestionAndOptions(QuestionAndAnswerModel model, string action)
        {
            int count = 0;
            ResultQuestionUpdateModel result = new ResultQuestionUpdateModel();
            QuestionModel questionModel = new QuestionModel();
            questionModel.Question = model.Question;
            questionModel.StoryID = model.StoryID;
            questionModel.UpdatedBy = (int)Session["UserID"];
            ResultModel resultQuestionModel = SaveQuestionDB(questionModel, action);

            if (resultQuestionModel != null)
            {
                count++;
                for (int i = 1; i <= 4; i++)
                {
                    AnswerModel answerModel1 = new AnswerModel();
                    answerModel1.QuestionID = resultQuestionModel.id;
                    answerModel1.Sequence = i;
                    answerModel1.IsCorrect = model.CorrectAnswer == i;

                    if (i == 1)
                        answerModel1.Option = model.Option1;
                    if (i == 2)
                        answerModel1.Option = model.Option2;
                    if (i == 3)
                        answerModel1.Option = model.Option3;
                    if (i == 4)
                        answerModel1.Option = model.Option4;

                    ResultModel resultAnswerModel1 = SaveAnswerDB(answerModel1, action);
                    if (resultAnswerModel1.success) count++;
                }

                result.success = true;
                result.message = "Successfully saved data.";
                result.question = GetQuestionsAndAnswersByQuestionID(resultQuestionModel.id);
            }

            return Json(result);
        }

        [HttpPost]
        public JsonResult UpdateAnswerSorting(string[] order)
        {
            ResultModel res = new ResultModel();
            var ansIDs = order.Select(id => int.Parse(id.Replace("ans-", ""))).ToList();

            for (int i = 0; i < ansIDs.Count; i++)
            {
                AnswerModel model = new AnswerModel();
                model.ID = ansIDs[i];
                model.Sequence = i + 1;
                res = SaveAnswerDB(model, "updateSortingOnly");
            }
            return Json(res);
        }

        [HttpPost]
        public JsonResult SaveQuestionOrAnswer(int questionid, int answerid, string edit_text, bool iscorrect, string edit)
        {
            ResultModel resultModel = new ResultModel();
            try
            {
                if (edit == "question")
                {
                    QuestionModel model = new QuestionModel();
                    model.ID = questionid;
                    model.Question = edit_text;
                    model.UpdatedBy = (int)Session["UserID"];
                    resultModel = SaveQuestionDB(model, "update");
                }
                else
                {
                    AnswerModel model = new AnswerModel();
                    model.ID = answerid;
                    model.QuestionID = questionid;
                    model.Option = edit_text;
                    model.IsCorrect = iscorrect;
                    resultModel = SaveAnswerDB(model, "update");
                }
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
                return Json(resultModel);
            }
            return Json(resultModel);
        }

        [HttpPost]
        public JsonResult SaveMultipleAnswerOptions(QuestionAndAnswerModel model, string action)
        {
            int count = 0;
            ResultQuestionUpdateModel result = new ResultQuestionUpdateModel();

            for (int i = 1; i <= 4; i++)
            {
                AnswerModel answerModel1 = new AnswerModel();
                answerModel1.QuestionID = model.QuestionID;
                answerModel1.Sequence = i;
                answerModel1.IsCorrect = model.CorrectAnswer == i;

                if (i == 1)
                    answerModel1.Option = model.Option1;
                if (i == 2)
                    answerModel1.Option = model.Option2;
                if (i == 3)
                    answerModel1.Option = model.Option3;
                if (i == 4)
                    answerModel1.Option = model.Option4;

                ResultModel resultAnswerModel1 = SaveAnswerDB(answerModel1, action);
                if (resultAnswerModel1.success) count++;
            }

            result.success = true;
            result.message = "Successfully saved data.";
            result.question = GetQuestionsAndAnswersByQuestionID(model.QuestionID);

            return Json(result);
        }




        private List<QuestionModel> GetQuestionsByStoryIDDB(int id)
        {
            List<QuestionModel> list = new List<QuestionModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select * from storyquestions where (isdeleted is null or isdeleted = 0) and storyid = @id order by id;";
            cmd.Parameters.AddWithValue("@id", id);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    QuestionModel question = new QuestionModel();
                    question.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    question.Question = rd["question"] != null ? rd["question"].ToString() : "";
                    question.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    question.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    question.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    question.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    question.Answers = GetAnswersByQuestionID(question.ID);
                    list.Add(question);
                }
                rd.Close();
            } 
            finally
            {
                con.Close();
            }
            return list;
        }

        private QuestionModel GetQuestionsByQuestionIDDB(int id)
        {
            QuestionModel question = new QuestionModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select * from storyquestions where id = @id;";
            cmd.Parameters.AddWithValue("@id", id);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                { 
                    question.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    question.Question = rd["question"] != null ? rd["question"].ToString() : "";
                    question.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    question.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    question.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    question.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    question.Answers = GetAnswersByQuestionID(question.ID);
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return question;
        }

        private List<AnswerModel> GetAnswersByQuestionID(int id)
        {
            List<AnswerModel> list = new List<AnswerModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "SELECT * FROM `storymultiplechoiceanswers` WHERE questionid = @id order by sequence;";
            cmd.Parameters.AddWithValue("@id", id);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    AnswerModel model = new AnswerModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.QuestionID = rd["questionid"] != null && rd["questionid"].ToString() != "" ? Convert.ToInt32(rd["questionid"].ToString()) : 0;
                    model.Option = rd["answer_option"] != null ? rd["answer_option"].ToString() : ""; 
                    model.IsCorrect = rd["iscorrect"] != null && rd["iscorrect"].ToString() != "" ? Convert.ToBoolean(rd["iscorrect"].ToString()) : false;
                    model.Sequence = rd["sequence"] != null && rd["sequence"].ToString() != "" ? Convert.ToInt32(rd["sequence"].ToString()) : 0;
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

        private ResultModel SaveQuestionDB(QuestionModel model, string action)
        {
            int count = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (action == "update")
            {
                cmd.CommandText = "UPDATE `storyquestions` SET `question`=@question,`updatedby`=@updatedby,`dateupdated`=@dateupdated WHERE `id` = @id";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@question", model.Question); 
                cmd.Parameters.AddWithValue("@updatedby", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@dateupdated", DateTime.Now);
            }
            else if (action == "delete")
            {
                resultActionMessage = action;
                cmd.CommandText = @"UPDATE `storyquestions` SET `isdeleted`=1, `deletedby`=@deletedby,`datedeleted`=@datedeleted WHERE `id` = @id;";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@deletedby", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@datedeleted", DateTime.Now);
            }
            else
            {
                cmd.CommandText = @"INSERT INTO `storyquestions`(`question`, `storyid`, `addedby`, `dateadded`, `updatedby`, `dateupdated`)  
                                                    VALUES (@question, @storyid, @addedby, @dateadded, @updatedby, @dateupdated); 
                                    SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@question", model.Question);
                cmd.Parameters.AddWithValue("@storyid", model.StoryID);
                cmd.Parameters.AddWithValue("@addedby", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@dateadded", DateTime.Now);
                cmd.Parameters.AddWithValue("@updatedby", model.UpdatedBy);
                cmd.Parameters.AddWithValue("@dateupdated", DateTime.Now);
            }

            try
            {
                con.Open();
                if (action == "add")
                { 
                    resultModel.id = Convert.ToInt32(cmd.ExecuteScalar());
                    count++;
                }
                else
                {
                    count = cmd.ExecuteNonQuery();
                }
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Question successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} question. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }

        private ResultModel SaveAnswerDB(AnswerModel model, string action)
        {
            int count = 0;
            string resultActionMessage = "save";
            ResultModel resultModel = new ResultModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();

            if (action == "update")
            {
                if(model.IsCorrect)
                {
                    cmd.CommandText = @"UPDATE `storymultiplechoiceanswers` SET `iscorrect` = 0 WHERE `questionid` = @questionid;
                                        UPDATE `storymultiplechoiceanswers` SET `answer_option`=@option,`iscorrect`=@iscorrect WHERE `id` = @id;";
                }
                else
                { 
                    cmd.CommandText = @"UPDATE `storymultiplechoiceanswers` SET `answer_option`=@option WHERE `id` = @id;";
                } 
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@questionid", model.QuestionID);
                cmd.Parameters.AddWithValue("@option", model.Option);
                cmd.Parameters.AddWithValue("@iscorrect", model.IsCorrect); 
            }  
            else if (action == "updateSortingOnly")
            {
                cmd.CommandText = @"UPDATE `storymultiplechoiceanswers` SET `sequence`=@sequence WHERE `id` = @id;";
                cmd.Parameters.AddWithValue("@id", model.ID);
                cmd.Parameters.AddWithValue("@sequence", model.Sequence);
            }
            else
            {
                cmd.CommandText = @"INSERT INTO `storymultiplechoiceanswers`(`questionid`, `answer_option`, `iscorrect`, `sequence`) VALUES (@questionid,@option,@iscorrect,@sequence)";
                cmd.Parameters.AddWithValue("@questionid", model.QuestionID);
                cmd.Parameters.AddWithValue("@option", model.Option);
                cmd.Parameters.AddWithValue("@iscorrect", model.IsCorrect);
                cmd.Parameters.AddWithValue("@sequence", model.Sequence); 
            }

            try
            {
                con.Open();
                count = cmd.ExecuteNonQuery();
                resultModel.success = count > 0;
                resultModel.message = count > 0 ? $"Anwer successfully {resultActionMessage}d." : $"Failed to {resultActionMessage} answer. Please check your data.";
            }
            catch (Exception ex)
            {
                resultModel.success = false;
                resultModel.message = ex.Message;
            }
            finally
            {
                con.Close();
            }
            return resultModel;
        }

        private QuestionModel GetQuestionsAndAnswersByQuestionID(int id)
        {
            QuestionModel question = new QuestionModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "select * from storyquestions where id = @id;";
            cmd.Parameters.AddWithValue("@id", id);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    question.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    question.Question = rd["question"] != null ? rd["question"].ToString() : "";
                    question.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    question.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    question.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    question.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    question.Answers = GetAnswersByQuestionID(question.ID);
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return question;
        } 
        #endregion Questions 




    }
}
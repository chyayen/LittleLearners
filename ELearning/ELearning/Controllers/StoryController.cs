using ELearning.Models;
using MySql.Data.MySqlClient;
using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Speech.AudioFormat;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Text.RegularExpressions; 
using PagedList;

namespace ELearning.Controllers
{
    public class StoryController : Controller
    {
        private string usertype = "student";
        string defaultConnection = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        // GET: Story
        public ActionResult Index()
        {
            if (Session["UserName"] == null)
            {
                // Store the current URL (for redirecting after login) 
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }
             
            List<StoryCategoryModel> list = new List<StoryCategoryModel>();
            list = GetStoryCategories();
             

            return View(list);
        }

        public ActionResult Category(int? id)
        {
            if (Session["UserName"] == null)
            {
                // Store the current URL (for redirecting after login) 
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if(id == null)
            {
                return HttpNotFound();
            }

            int classID = (int)Session["ClassID"];
            int studentID = (int)Session["UserID"];
            int sectionID = (int)Session["SectionID"];
            List<StoryModel> list = new List<StoryModel>();
            list = GetAllStoriesByStudent(id.Value, classID, studentID);

            if (list.Count == 0)
                GetAllStoriesBySection(id.Value, classID, sectionID, studentID);

            return View(list);
        }

        public ActionResult Detail(int? id)
        {
            if (Session["UserName"] == null)
            {
                // Store the current URL (for redirecting after login)
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var studentid = (int)Session["UserID"];
            StoryModel model = GetStoryByIDDB(id.Value, studentid);

            return View(model);
        } 

        public ActionResult Question(int? id)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            StoryModel course = GetStoriesByID(id.Value);
            QuestionStudentViewModel model = new QuestionStudentViewModel();
            model.Questions = GetQuestionsByStoryID(id.Value);
            model.StoryID = id.Value;
            model.StoryTitle = course.Title;

            return View(model);
        }
         
        [HttpPost]
        public JsonResult QuizSubmission(QuestionStudentAnswerViewModel model)
        {
            ResultModel resultModel = new ResultModel();
            DateTime datenow = DateTime.Now;
            int count = 0;
             
            if(model.QuestionResults != null && model.QuestionResults.Count > 0)
            {
                var attempt = GetStudentAnswerAttemptByStory(model.StudentID, model.StoryID);
                foreach (var qr in model.QuestionResults)
                { 
                    count += AddStudentAnswerDB(qr.QuestionID, model.StudentID, qr.Answer, datenow, (attempt + 1));
                }

                if(count > 0)
                {
                    StudentProgressFalseRetake(model.StoryID, model.StudentID);
                    resultModel.success = true;
                    resultModel.message = "Answers successfully submitted. Please wait for your teacher to check.";
                }
                else
                {
                    resultModel.success = false;
                    resultModel.message = "Failed to submit answers. Please check again.";
                }
            }
            else
            {
                resultModel.success = false;
                resultModel.message = "Failed to submit answers. Please check again.";
            }

            return Json(resultModel);
        }

        public JsonResult ViewQuizResult(int storyid)
        { 
            QuizAssessmentModel resultModel = new QuizAssessmentModel();
            AnswerComputeModel model = new AnswerComputeModel();
            var studentid = (int)Session["UserID"];
            model = GetQuizAssessmentDB(studentid, storyid, 1);

            if (model != null)
            {
                resultModel.QuizAssessmentPercentage = (int)Math.Round(((decimal)model.CountCorrectAnswer / model.CountTotalQuestion) * 100);

                if (resultModel.QuizAssessmentPercentage >= 60)
                {
                    resultModel.QuizAssessmentIcon = "success";
                    resultModel.QuizAssessmentMessage = "Nice job, you passed!";
                }
                else
                {
                    resultModel.QuizAssessmentIcon = "error";
                    resultModel.QuizAssessmentMessage = "Sorry, you didn't pass.";
                }
            }
            else
            {
                resultModel.QuizAssessmentPercentage = 0;
                resultModel.QuizAssessmentIcon = "info";
                resultModel.QuizAssessmentMessage = "There was an error when retrieving your quiz result.";
            }


            return Json(resultModel, JsonRequestBehavior.AllowGet);
        }

        public ActionResult QuizHistory(int? page)
        {
            if (Session["UserName"] == null)
            {
                // Store the current URL (for redirecting after login)
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }

            int pageSize = 25;
            int pageNumber = (page ?? 1);
            var studentid = (int)Session["UserID"];
            var stories = GetStoryByStudentDB(studentid);

            QuizHistoryViewModel model = new QuizHistoryViewModel();
            model.Stories = stories.ToPagedList(pageNumber, pageSize); 

            return View(model);
        }
        public ActionResult QuizHistoryAttempt(int? id)
        {
            if (Session["UserName"] == null)
            {
                // Store the current URL (for redirecting after login)
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var studentid = (int)Session["UserID"];
            var list = GetQuizResultByStoryDB(studentid, id.Value);

            QuizGradeByStudentViewModel model = new QuizGradeByStudentViewModel();
            model.QuizGradeList = list;
            model.StoryTitle = list != null && list.Count > 0 ? list.Select(l => l.StoryTitle).FirstOrDefault() : "";

            return View(model);
        }
        public ActionResult QuizHistoryAttemptDetail(int? id, int? attempt)
        {
            if (Session["UserName"] == null)
            {
                // Store the current URL (for redirecting after login)
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.AbsoluteUri });
            }

            if (id == null)
            {
                return HttpNotFound();
            }

            var studentid = (int)Session["UserID"];
            var list = GetDetailedQuizResultByStoryDB(studentid, id.Value, attempt.Value);

            DetailedQuizGradeByStudentViewModel model = new DetailedQuizGradeByStudentViewModel();
            model.QuizGradeDetailedList = list; 

            return View(model);
        }

        [HttpPost]
        public JsonResult SaveProgress(int StoryID, int StudentID, int LastPageRead, int TotalPages)
        {
            try
            {
                StudentProgressModel model = new StudentProgressModel();
                model.StoryID = StoryID;
                model.StudentID = StudentID;
                model.TotalPages = TotalPages;

                var progress = GetStudentProgress(StudentID, StoryID);
                if (progress == null)
                {
                    // If no progress entry exists, create a new one 
                    model.LastPageRead = LastPageRead;
                    model.Status = LastPageRead == -1 ? "Not Started" : "In Progress";
                }
                else
                {
                    // If entry exists, update the LastPageRead and Status 
                    model.LastPageRead = LastPageRead;
                    model.Status = (LastPageRead == -1) ? "Not Started" :
                                      (LastPageRead == progress.TotalPages ? "Completed" : "In Progress");
                }

                // Save changes to the database 
                UpdateStudentProgress(model);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult SaveStudentRandomAnswer(int StoryID, int StudentID, string Answer)
        {
            try
            {
                var DateAnswered = DateTime.Now;
                int count = AddStudentRandomAnswerDB(StoryID, StudentID, Answer, DateAnswered);

                return Json(new { success = count > 0 });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }



        private List<StoryCategoryModel> GetStoryCategories()
        {
            List<StoryCategoryModel> list = new List<StoryCategoryModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand(); 
            cmd.CommandText = @"SELECT * FROM `storycategories` where isactive = 1 order by sequence"; 

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StoryCategoryModel model = new StoryCategoryModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Code = rd["code"] != null ? rd["code"].ToString() : "";
                    model.Name = rd["name"] != null ? rd["name"].ToString() : "";
                    model.Sequence = rd["sequence"] != null && rd["sequence"].ToString() != "" ? Convert.ToInt32(rd["sequence"].ToString()) : 0;
                    model.Icon = rd["icon"] != null ? rd["icon"].ToString() : "";
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

        private List<StoryModel> GetAllStoriesByStudent(int storycategoryid, int classID, int studentid)
        {
            List<StoryModel> list = new List<StoryModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            //cmd.CommandText = @"select c.*, u.name as addedbyname from stories c left join users u on c.addedby = u.id where (c.isdeleted is null or c.isdeleted = 0) and c.classid = @classid order by c.id desc;";
            cmd.CommandText = @"select distinct s.*, u.name as addedbyname, ssp.lastpageread, ssp.totalpages, ssp.status, ssp.allowretake, sc.id as storycategoryid
                                    , (select MAX(attempt) from studentanswers sa inner join storyquestions sq on sq.id = sa.questionid where sa.studentid = @studentid and sq.storyid = s.id) as attempt
                                    , (select sa.checkby from studentanswers sa inner join storyquestions sq on sq.id = sa.questionid where sa.studentid = @studentid and sq.storyid = s.id order by sa.attempt desc limit 1) as checkby
                                from stories s 
                                inner join users u on s.addedby = u.id 
                                ";

            //if (storycategoryid != 3)
            //{
                cmd.CommandText += "inner join storystudentassignments ssa on ssa.storyid = s.id";
            //}

            cmd.CommandText += @"
                                left join studentstoryprogress ssp on ssp.studentid = @studentid and ssp.storyid = s.id
                                left join storycategories sc on sc.isactive = 1 and sc.code = ssp.status
                                where s.classid = @classid 
                                ";

            //if (storycategoryid == 3)
            //{
            //    cmd.CommandText += " and ssp.studentid = @studentid";
            //}
            //else
            //{ 
            //    //cmd.CommandText += " and ssa.sectionid in (select sectionid from studentclasses where studentid = @studentid)";
                cmd.CommandText += " and ssa.studentid = @studentid and (s.isdeleted is null or s.isdeleted = 0)";
            //}

            cmd.CommandText += @" 
                                and (case when sc.id is null then 1 else sc.id end) = @storycategoryid 
                                order by s.id desc;";
            // and (case when s.incomplete = 1 then 5 when sc.id is null then 1 else sc.id end) = @storycategoryid 

            cmd.Parameters.AddWithValue("@classid", classID);
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storycategoryid", storycategoryid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StoryModel model = new StoryModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Title = rd["title"] != null ? rd["title"].ToString() : "";
                    model.CoverImageName = rd["coverimage"] != null ? rd["coverimage"].ToString() : "";
                    model.Content = rd["content"] != null ? rd["content"].ToString() : "";
                    model.AddedByName = rd["addedbyname"] != null ? rd["addedbyname"].ToString() : "";
                    model.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    model.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    model.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    model.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    model.DeletedBy = rd["deletedby"] != null && rd["deletedby"].ToString() != "" ? Convert.ToInt32(rd["deletedby"].ToString()) : 0;
                    model.DateDeleted = rd["datedeleted"] != null && rd["datedeleted"].ToString() != "" ? Convert.ToDateTime(rd["datedeleted"].ToString()) : new DateTime(2000, 1, 1);
                    model.IsDeleted = rd["isdeleted"] != null && rd["isdeleted"].ToString() != "" ? Convert.ToBoolean(rd["isdeleted"].ToString()) : false;
                    model.QuizAttempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0;
                    model.AllowToRetake = rd["allowretake"] != null && rd["allowretake"].ToString() != "" ? Convert.ToBoolean(rd["allowretake"].ToString()) : false;
                    model.CheckedBy = rd["checkby"] != null && rd["checkby"].ToString() != "" ? Convert.ToInt32(rd["checkby"].ToString()) : 0;

                    model.Progress = new StudentProgressModel()
                    {
                        StudentID = studentid,
                        StoryID = model.ID,
                        LastPageRead = rd["lastpageread"] != null && rd["lastpageread"].ToString() != "" ? Convert.ToInt32(rd["lastpageread"].ToString()) : 0,
                        TotalPages = rd["totalpages"] != null && rd["totalpages"].ToString() != "" ? Convert.ToInt32(rd["totalpages"].ToString()) : 0,
                        Status = rd["status"] != null ? rd["status"].ToString() : "Not Started",
                        StoryCategoryID = rd["storycategoryid"] != null && rd["storycategoryid"].ToString() != "" ? Convert.ToInt32(rd["storycategoryid"].ToString()) : 0,
                    };

                    if (model.IsDeleted)
                    {
                        model.Title = model.Title + " <span class='badge rounded-pill bg-danger' style='font-size:12px;'>deleted</span>";
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

        private List<StoryModel> GetAllStoriesBySection(int storycategoryid, int classID, int sectionid, int studentid)
        {
            List<StoryModel> list = new List<StoryModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            //cmd.CommandText = @"select c.*, u.name as addedbyname from stories c left join users u on c.addedby = u.id where (c.isdeleted is null or c.isdeleted = 0) and c.classid = @classid order by c.id desc;";
            cmd.CommandText = @"select distinct s.*, u.name as addedbyname, ssp.lastpageread, ssp.totalpages, ssp.status, ssp.allowretake, sc.id as storycategoryid
                                    , (select MAX(attempt) from studentanswers sa inner join storyquestions sq on sq.id = sa.questionid where sa.studentid = @studentid and sa.checkby = 0 and sq.storyid = s.id) as attempt
                                from stories s 
                                inner join users u on s.addedby = u.id 
                                inner join storystudentassignments ssa on ssa.storyid = s.id
                                left join studentstoryprogress ssp on ssp.studentid = @studentid and ssp.storyid = s.id
                                left join storycategories sc on sc.isactive = 1 and sc.code = ssp.status
                                where (s.isdeleted is null or s.isdeleted = 0) and s.classid = @classid 
	                                and ssa.sectionid in (select sectionid from studentclasses where studentid = @sectionid)
                                    and (case when sc.id is null then 1 else sc.id end) = @storycategoryid
                                order by s.id desc;";
            //and (case when s.incomplete = 1 then 5 when sc.id is null then 1 else sc.id end) = @storycategoryid

            cmd.Parameters.AddWithValue("@classid", classID);
            cmd.Parameters.AddWithValue("@sectionid", sectionid);
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storycategoryid", storycategoryid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    StoryModel model = new StoryModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Title = rd["title"] != null ? rd["title"].ToString() : "";
                    model.CoverImageName = rd["coverimage"] != null ? rd["coverimage"].ToString() : "";
                    model.Content = rd["content"] != null ? rd["content"].ToString() : "";
                    model.AddedByName = rd["addedbyname"] != null ? rd["addedbyname"].ToString() : "";
                    model.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    model.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    model.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    model.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    model.DeletedBy = rd["deletedby"] != null && rd["deletedby"].ToString() != "" ? Convert.ToInt32(rd["deletedby"].ToString()) : 0;
                    model.DateDeleted = rd["datedeleted"] != null && rd["datedeleted"].ToString() != "" ? Convert.ToDateTime(rd["datedeleted"].ToString()) : new DateTime(2000, 1, 1);
                    model.IsDeleted = rd["isdeleted"] != null && rd["isdeleted"].ToString() != "" ? Convert.ToBoolean(rd["isdeleted"].ToString()) : false;
                    model.QuizAttempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0;
                    model.AllowToRetake = rd["allowretake"] != null && rd["allowretake"].ToString() != "" ? Convert.ToBoolean(rd["allowretake"].ToString()) : false;

                    model.Progress = new StudentProgressModel()
                    { 
                        StoryID = model.ID,
                        LastPageRead = rd["lastpageread"] != null && rd["lastpageread"].ToString() != "" ? Convert.ToInt32(rd["lastpageread"].ToString()) : 0,
                        TotalPages = rd["totalpages"] != null && rd["totalpages"].ToString() != "" ? Convert.ToInt32(rd["totalpages"].ToString()) : 0,
                        Status = rd["status"] != null ? rd["status"].ToString() : "Not Started",
                        StoryCategoryID = rd["storycategoryid"] != null && rd["storycategoryid"].ToString() != "" ? Convert.ToInt32(rd["storycategoryid"].ToString()) : 0,
                    };

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

        private StoryModel GetStoryByIDDB(int id, int studentid)
        {
            StoryModel model = new StoryModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select s.*, sq.attempt, ssp.lastpageread, ssp.totalpages, ssp.status, sc.id as storycategoryid, ssp.allowretake 
                                from stories s 
                                left join (
	                                select sq.storyid, MAX(sa.attempt) as attempt 
                                    from storyquestions sq
                                    inner join studentanswers sa on sa.questionid = sq.id
                                    where sa.studentid = @studentid
                                    group by sq.storyid
                                ) sq on sq.storyid = s.id
                                left join studentstoryprogress ssp on ssp.studentid = @studentid and ssp.storyid = s.id
                                left join storycategories sc on sc.isactive = 1 and sc.code = ssp.status
                                where s.id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@studentid", studentid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                { 
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.Title = rd["title"] != null ? rd["title"].ToString() : "";
                    model.Content = rd["content"] != null ? rd["content"].ToString() : ""; 
                    model.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    model.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    model.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    model.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    model.DeletedBy = rd["deletedby"] != null && rd["deletedby"].ToString() != "" ? Convert.ToInt32(rd["deletedby"].ToString()) : 0;
                    model.DateDeleted = rd["datedeleted"] != null && rd["datedeleted"].ToString() != "" ? Convert.ToDateTime(rd["datedeleted"].ToString()) : new DateTime(2000, 1, 1);
                    model.IsDeleted = rd["isdeleted"] != null && rd["isdeleted"].ToString() != "" ? Convert.ToBoolean(rd["isdeleted"].ToString()) : false;
                    model.Incomplete = rd["incomplete"] != null && rd["incomplete"].ToString() != "" ? Convert.ToBoolean(rd["incomplete"].ToString()) : false;
                    model.QuizAttempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0;
                    model.AllowToRetake = rd["allowretake"] != null && rd["allowretake"].ToString() != "" ? Convert.ToBoolean(rd["allowretake"].ToString()) : false;
                    model.RandomQuestion = rd["randomquestion"] != null ? rd["randomquestion"].ToString() : "";
                    model.RandomAnswerOption1 = rd["randomansweroption1"] != null ? rd["randomansweroption1"].ToString() : "";
                    model.RandomAnswerOption2 = rd["randomansweroption2"] != null ? rd["randomansweroption2"].ToString() : "";
                    model.RandomAnswerOption3 = rd["randomansweroption3"] != null ? rd["randomansweroption3"].ToString() : "";
                    model.RandomAnswerOption4 = rd["randomansweroption4"] != null ? rd["randomansweroption4"].ToString() : "";
                    model.RandomCorrectAnswer = rd["randomcorrectanswer"] != null ? rd["randomcorrectanswer"].ToString() : "";
                    model.RandomQuestionHint = rd["randomquestionhint"] != null ? rd["randomquestionhint"].ToString() : "";
                    model.Subtitle = rd["subtitle"] != null ? rd["subtitle"].ToString() : "";

                    if (!string.IsNullOrEmpty(model.Content))
                    {
                        // Character limit per page
                        int characterLimit = 1500;

                        // Split the content into pages
                        model.PageContents = CreatePages(model.Content, characterLimit);
                    }

                    model.Progress = new StudentProgressModel()
                    {
                        StoryID = model.ID,
                        LastPageRead = rd["lastpageread"] != null && rd["lastpageread"].ToString() != "" ? Convert.ToInt32(rd["lastpageread"].ToString()) : 0,
                        TotalPages = rd["totalpages"] != null && rd["totalpages"].ToString() != "" ? Convert.ToInt32(rd["totalpages"].ToString()) : 0,
                        Status = rd["status"] != null ? rd["status"].ToString() : "Not Started",
                        StoryCategoryID = rd["storycategoryid"] != null && rd["storycategoryid"].ToString() != "" ? Convert.ToInt32(rd["storycategoryid"].ToString()) : 0,
                    };
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return model;
        }

        private List<StoryModel> GetStoryByStudentDB(int studentid)
        {
            List<StoryModel> list = new List<StoryModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select s.*, sq.attempt from stories s 
                                left join (
	                                select sq.storyid, sa.studentid, MAX(sa.attempt) as attempt 
                                    from storyquestions sq
                                    inner join studentanswers sa on sa.questionid = sq.id 
                                    group by sq.storyid, sa.studentid
                                ) sq on sq.storyid = s.id
                                where sq.studentid = @studentid
                                order by s.dateupdated desc"; //(s.isdeleted is null or s.isdeleted = 0)
            cmd.Parameters.AddWithValue("@studentid", studentid);

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
                    model.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    model.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    model.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    model.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    model.DeletedBy = rd["deletedby"] != null && rd["deletedby"].ToString() != "" ? Convert.ToInt32(rd["deletedby"].ToString()) : 0;
                    model.DateDeleted = rd["datedeleted"] != null && rd["datedeleted"].ToString() != "" ? Convert.ToDateTime(rd["datedeleted"].ToString()) : new DateTime(2000, 1, 1);
                    model.IsDeleted = rd["isdeleted"] != null && rd["isdeleted"].ToString() != "" ? Convert.ToBoolean(rd["isdeleted"].ToString()) : false;
                    model.QuizAttempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0;

                    if(model.IsDeleted)
                    {
                        model.Title = model.Title + " <span class='badge rounded-pill bg-danger'>deleted</span>";
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
         
        private List<QuestionModel> GetQuestionsByStoryID(int id)
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

        private int AddStudentAnswerDB(int questionid, int studentid, string answer, DateTime dateanswered , int attempt)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"INSERT INTO `studentanswers`(`questionid`, `studentid`, `answer`, `dateanswered`, `attempt`) VALUES (@questionid, @studentid, @answer, @dateanswered, @attempt)";
            cmd.Parameters.AddWithValue("@questionid", questionid);
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@answer", (answer == null ? "" : answer));
            cmd.Parameters.AddWithValue("@dateanswered", dateanswered);
            cmd.Parameters.AddWithValue("@attempt", attempt);

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

        private int StudentProgressFalseRetake(int storyid, int studentid)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"UPDATE `studentstoryprogress` SET `allowretake`= 0
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

        private int GetStudentAnswerAttemptByStory(int studentid, int storyid)
        {
            int attempt = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"select MAX(sa.attempt) as attempt from studentanswers sa 
                                inner join storyquestions sq on sq.id = sa.questionid
                                where sa.studentid = @studentid and sq.storyid = @storyid";
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                { 
                    attempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0; 
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return attempt;
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
                    model.AddedBy = rd["addedby"] != null && rd["dateadded"].ToString() != "" ? Convert.ToInt32(rd["addedby"].ToString()) : 0;
                    model.DateAdded = rd["dateadded"] != null && rd["dateadded"].ToString() != "" ? Convert.ToDateTime(rd["dateadded"].ToString()) : new DateTime(2000, 1, 1);
                    model.UpdatedBy = rd["updatedby"] != null && rd["updatedby"].ToString() != "" ? Convert.ToInt32(rd["updatedby"].ToString()) : 0;
                    model.DateUpdated = rd["dateupdated"] != null && rd["dateupdated"].ToString() != "" ? Convert.ToDateTime(rd["dateupdated"].ToString()) : new DateTime(2000, 1, 1);
                    model.DeletedBy = rd["deletedby"] != null && rd["deletedby"].ToString() != "" ? Convert.ToInt32(rd["deletedby"].ToString()) : 0;
                    model.DateDeleted = rd["datedeleted"] != null && rd["datedeleted"].ToString() != "" ? Convert.ToDateTime(rd["datedeleted"].ToString()) : new DateTime(2000, 1, 1);
                    model.IsDeleted = rd["isdeleted"] != null && rd["isdeleted"].ToString() != "" ? Convert.ToBoolean(rd["isdeleted"].ToString()) : false;
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

        private List<PageContent> CreatePages(string content, int charLimit)
        {
            var pages = new List<PageContent>();
            int pageNumber = 1;

            //while (content.Length > 0)
            //{
            //    // Ensure we don't exceed the character limit
            //    string pageContent = content.Substring(0, Math.Min(charLimit, content.Length));
            //    pages.Add(new PageContent
            //    {
            //        PageNumber = pageNumber,
            //        Title = $"Page {pageNumber}",
            //        Content = pageContent
            //    });

            //    // Move to the next page, removing processed content
            //    content = content.Substring(pageContent.Length);
            //    pageNumber++;
            //}




            // Updated Regex pattern to match <p class="fb-page-content"> tag and capture its content
            string pattern = @"<p class=""fb-page-content""[^>]*>(.*?)<\/p>";

            // Find all matches
            MatchCollection matches = Regex.Matches(content, pattern, RegexOptions.Singleline);

            // List to store grouped paragraphs
            List<string> groupedParagraphs = new List<string>();

            // Function to check if content is empty or only contains <br> tags
            bool IsEmptyOrBrTag(string text)
            {
                string trimmedContent = text.Trim();
                return string.IsNullOrEmpty(trimmedContent) || trimmedContent == "<br>" || Regex.IsMatch(trimmedContent, @"^(<br\s*/?>)+$", RegexOptions.IgnoreCase);
            }

            // Loop through and group every two paragraphs
            for (int i = 0; i < matches.Count; i += 3)
            {
                // Extract the inner content of the current and next <p> tags
                string currentContent = matches[i].Groups[1].Value.Trim();
                string nextContent = i + 1 < matches.Count ? matches[i + 1].Groups[1].Value.Trim() : string.Empty;
                string thirdContent = i + 2 < matches.Count ? matches[i + 2].Groups[1].Value.Trim() : string.Empty;

                // Only include the <p> tag if it contains text other than <br>
                string combinedGroup = string.Empty;
                if (!IsEmptyOrBrTag(currentContent))
                {
                    combinedGroup = matches[i].Value; // Add the first <p> tag
                }
                if (!IsEmptyOrBrTag(nextContent))
                {
                    combinedGroup += matches[i + 1].Value; // Add the second <p> tag if it exists and contains valid content
                }

                if (!IsEmptyOrBrTag(thirdContent))
                {
                    combinedGroup += matches[i + 2].Value;
                }

                // If combinedGroup contains valid content, add it to the list
                if (!string.IsNullOrEmpty(combinedGroup))
                {
                    groupedParagraphs.Add(combinedGroup); // Add the combined group to the list
                }
            }

            // Output each group of paragraphs
            foreach (var group in groupedParagraphs)
            {
                pages.Add(new PageContent
                {
                    PageNumber = pageNumber,
                    Title = $"Page {pageNumber}",
                    Content = group
                });

                // Move to the next page
                pageNumber++;
            }

            return pages;
        }



        private int InsertStudentAnswer(GradeModel model)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO `grades`(`studentid`, `storyid`, `questionid`, `stud_answerid`, `attempt`, `dateadded`) " +
                                           "VALUES (@studentid, @storyid, @questionid, @stud_answerid, @attempt, @dateadded)";
            cmd.Parameters.AddWithValue("@studentid", model.StudentID);
            cmd.Parameters.AddWithValue("@storyid", model.StoryID);
            cmd.Parameters.AddWithValue("@questionid", model.QuestionID);
            cmd.Parameters.AddWithValue("@stud_answerid", model.StudentAnswerID);
            cmd.Parameters.AddWithValue("@attempt", model.Attempt);
            cmd.Parameters.AddWithValue("@dateadded", DateTime.Now);

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

        private int GetStudentMaxAttemptInQuestion(int studentid, int storyid)
        {
            int attempt = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = "SELECT MAX(attempt) attempt FROM grades where studentid = @studentid and storyid = @storyid";
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                { 
                    attempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0; 
                }
            }
            finally
            {
                con.Close();
            }
            return attempt;
        }

        private AnswerComputeModel GetQuizAssessmentDB(int studentid, int storyid, int attempt)
        { 
            AnswerComputeModel model = new AnswerComputeModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT SUM(case when a.iscorrect is null or a.iscorrect = 0 then 0 else 1 end) as CntCorrect, (select COUNT(*) from `questions` gg where (gg.isdeleted is null or gg.isdeleted = 0) and gg.courseid = g.storyid) as CntTotal 
                                FROM `grades` g INNER JOIN `answers` a on g.stud_answerid = a.id 
                                WHERE g.attempt = @attempt and g.studentid = @studentid and g.storyid = @storyid;";
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@attempt", attempt); 

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    model.CountCorrectAnswer = rd["CntCorrect"] != null && rd["CntCorrect"].ToString() != "" ? Convert.ToInt32(rd["CntCorrect"].ToString()) : 0;
                    model.CountTotalQuestion = rd["CntTotal"] != null && rd["CntTotal"].ToString() != "" ? Convert.ToInt32(rd["CntTotal"].ToString()) : 0; 
                }
            }
            finally
            {
                con.Close();
            }
            return model;
        }

        private List<QuizGradeByStudentModel> GetQuizResultByStoryDB(int studentid, int storyid)
        {
            List<QuizGradeByStudentModel> list = new List<QuizGradeByStudentModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT distinct sg.ID, sg.storyid, sg.studentid, sa.attempt, sg.totalquestions, sg.grade, sg.remarks, sg.checkedbyname, sg.datechecked
	                                , (select title from stories where id = @storyid) as storytitle   
                                FROM `studentanswers` sa
                                LEFT JOIN (
	                                SELECT sg.*, u.name as checkedbyname 
                                    FROM `studentgrades` sg
                                    INNER JOIN users u on u.usertype = 'teacher' and sg.checkedby = u.id 
                                    WHERE sg.storyid = @storyid 
                                ) sg on sg.attempt = sa.attempt and sg.studentid = sa.studentid
                                where sa.studentid = @studentid and sa.questionid in (select sq.id from storyquestions sq where sq.storyid = @storyid)
                                order by sg.attempt desc";
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    QuizGradeByStudentModel model = new QuizGradeByStudentModel();
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0; 
                    model.StudentID = rd["studentid"] != null && rd["studentid"].ToString() != "" ? Convert.ToInt32(rd["studentid"].ToString()) : 0; 
                    model.StoryID = rd["storyid"] != null && rd["storyid"].ToString() != "" ? Convert.ToInt32(rd["storyid"].ToString()) : 0;
                    model.StoryTitle = rd["storytitle"] != null ? rd["storytitle"].ToString() : "";
                    model.Attempt = rd["attempt"] != null && rd["attempt"].ToString() != "" ? Convert.ToInt32(rd["attempt"].ToString()) : 0;
                    model.TotalQuestions = rd["totalquestions"] != null && rd["totalquestions"].ToString() != "" ? Convert.ToInt32(rd["totalquestions"].ToString()) : 0;
                    model.Grade = rd["grade"] != null && rd["grade"].ToString() != "" ? Convert.ToInt32(rd["grade"].ToString()) : 0;
                    model.Remarks = rd["remarks"] != null ? rd["remarks"].ToString() : "";
                    model.DateChecked = rd["datechecked"] != null && rd["datechecked"].ToString() != "" ? Convert.ToDateTime(rd["datechecked"].ToString()) : new DateTime(2000, 1, 1); 
                    model.CheckedBy = rd["checkedbyname"] != null ? rd["checkedbyname"].ToString() : "";

                    if (model.TotalQuestions > 0)
                    {
                        decimal percentage = (Convert.ToDecimal(model.Grade) / (Convert.ToDecimal(model.TotalQuestions) * 10)) * 100;
                        model.GradePercentage = Math.Round(percentage, 2); // Rounds to 2 decimal places 
                    }
                    else
                    {
                        model.GradePercentage = 0;
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

        private List<DetailedQuizGradeByStudentModel> GetDetailedQuizResultByStoryDB(int studentid, int storyid, int attempt)
        {
            List<DetailedQuizGradeByStudentModel> list = new List<DetailedQuizGradeByStudentModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT sa.questionid, sq.question, sa.answer, sa.dateanswered, sa.score, sg.storyid, sg.studentid, sa.attempt, sg.totalquestions, sg.grade, sg.remarks, sg.checkedbyname, sg.datechecked
	                                , (select title from stories where id = @storyid) as storytitle  
                                FROM `studentanswers` sa
                                INNER JOIN `storyquestions` sq on sq.id = sa.questionid
                                LEFT JOIN (
	                                SELECT sg.*, u.name as checkedbyname 
                                    FROM `studentgrades` sg
                                    INNER JOIN users u on u.usertype = 'teacher' and sg.checkedby = u.id  
                                ) sg on sg.attempt = sa.attempt and sg.studentid = sa.studentid and sg.storyid = sq.storyid 
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

                    if (model.TotalQuestions > 0)
                    {
                        decimal percentage = (Convert.ToDecimal(model.Grade) / (Convert.ToDecimal(model.TotalQuestions) * 10)) * 100;
                        model.GradePercentage = Math.Round(percentage, 2); // Rounds to 2 decimal places 
                    }
                    else
                    {
                        model.GradePercentage = 0;
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


        private StudentProgressModel GetStudentProgress(int studentid, int storyid)
        {
            StudentProgressModel model = new StudentProgressModel();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT * FROM `studentstoryprogress` WHERE `studentid` = @studentid and `storyid` = @storyid";
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@storyid", storyid);

            try
            {
                con.Open();
                MySqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    model.ID = rd["id"] != null && rd["id"].ToString() != "" ? Convert.ToInt32(rd["id"].ToString()) : 0;
                    model.StudentID = rd["studentid"] != null && rd["studentid"].ToString() != "" ? Convert.ToInt32(rd["studentid"].ToString()) : 0;
                    model.StoryID = rd["storyid"] != null && rd["storyid"].ToString() != "" ? Convert.ToInt32(rd["storyid"].ToString()) : 0; 
                    model.LastPageRead = rd["lastpageread"] != null && rd["lastpageread"].ToString() != "" ? Convert.ToInt32(rd["lastpageread"].ToString()) : 0;
                    model.TotalPages = rd["totalpages"] != null && rd["totalpages"].ToString() != "" ? Convert.ToInt32(rd["totalpages"].ToString()) : 0;
                    model.Status = rd["status"] != null ? rd["status"].ToString() : "";                      
                }
                rd.Close();
            }
            finally
            {
                con.Close();
            }
            return model;
        }

        private int UpdateStudentProgress(StudentProgressModel model)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"IF (SELECT COUNT(*) FROM `studentstoryprogress` WHERE `studentid` = @studentid and `storyid` = @storyid) > 0 THEN 
                                        UPDATE `studentstoryprogress` 
                                        SET `lastpageread` = @lastpageread, `totalpages` = @totalpages, `status` = @status
                                        WHERE `studentid` = @studentid and `storyid` = @storyid;
                                    ELSE 
                                        INSERT INTO `studentstoryprogress`(`storyid`, `studentid`, `lastpageread`, `totalpages`, `status`) VALUES (@storyid, @studentid, @lastpageread, @totalpages, @status);
                                    END IF;
                                "; 
            cmd.Parameters.AddWithValue("@storyid", model.StoryID);
            cmd.Parameters.AddWithValue("@studentid", model.StudentID);
            cmd.Parameters.AddWithValue("@lastpageread", model.LastPageRead);
            cmd.Parameters.AddWithValue("@totalpages", model.TotalPages);
            cmd.Parameters.AddWithValue("@status", model.Status); 

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

        private int AddStudentRandomAnswerDB(int storyid, int studentid, string answer, DateTime dateanswered)
        {
            int count = 0;
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"IF (select COUNT(*) from studentrandomanswers where studentid = @studentid and storyid = @storyid) > 0 THEN   
                                        update studentrandomanswers set answer = @answer, dateanswered = @dateanswered
                                        where studentid = @studentid and storyid = @storyid;
                                ELSE
                                    INSERT INTO `studentrandomanswers`(`storyid`, `studentid`, `answer`, `dateanswered`) VALUES (@storyid, @studentid, @answer, @dateanswered);
                                 END IF";
            cmd.Parameters.AddWithValue("@storyid", storyid);
            cmd.Parameters.AddWithValue("@studentid", studentid);
            cmd.Parameters.AddWithValue("@answer", (answer == null ? "" : answer));
            cmd.Parameters.AddWithValue("@dateanswered", dateanswered); 

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

        #region Read Story Content
        public FileResult TextToMp3(string text)
        {
            //Primary memory stream for storing mp3 audio
            var mp3Stream = new MemoryStream();
            //Speech format
            var speechAudioFormatConfig = new SpeechAudioFormatInfo
            (samplesPerSecond: 8000, bitsPerSample: AudioBitsPerSample.Sixteen,
            channel: AudioChannel.Stereo);
            //Naudio's wave format used for mp3 conversion. 
            //Mirror configuration of speech config.
            var waveFormat = new WaveFormat(speechAudioFormatConfig.SamplesPerSecond,
            speechAudioFormatConfig.BitsPerSample, speechAudioFormatConfig.ChannelCount);
            try
            {
                //Build a voice prompt to have the voice talk slower 
                //and with an emphasis on words
                var prompt = new PromptBuilder
                { Culture = CultureInfo.CreateSpecificCulture("en-US") };
                prompt.StartVoice(prompt.Culture);
                prompt.StartSentence();
                prompt.StartStyle(new PromptStyle()
                { Emphasis = PromptEmphasis.Reduced, Rate = PromptRate.Slow });
                prompt.AppendText(text);
                prompt.EndStyle();
                prompt.EndSentence();
                prompt.EndVoice();

                //Wav stream output of converted text to speech
                using (var synthWavMs = new MemoryStream())
                {
                    //Spin off a new thread that's safe for an ASP.NET application pool.
                    var resetEvent = new ManualResetEvent(false);
                    ThreadPool.QueueUserWorkItem(arg =>
                    {
                        try
                        {
                            //initialize a voice with standard settings
                            var siteSpeechSynth = new SpeechSynthesizer();
                            //Set memory stream and audio format to speech synthesizer
                            siteSpeechSynth.SetOutputToAudioStream
                                (synthWavMs, speechAudioFormatConfig);
                            //build a speech prompt
                            siteSpeechSynth.Speak(prompt);
                        }
                        catch (Exception ex)
                        {
                            //This is here to diagnostic any issues with the conversion process. 
                            //It can be removed after testing.
                            Response.AddHeader
                            ("EXCEPTION", ex.GetBaseException().ToString());
                        }
                        finally
                        {
                            resetEvent.Set();//end of thread
                        }
                    });
                    //Wait until thread catches up with us
                    WaitHandle.WaitAll(new WaitHandle[] { resetEvent });
                    //Estimated bitrate
                    var bitRate = (speechAudioFormatConfig.AverageBytesPerSecond * 8);
                    //Set at starting position
                    synthWavMs.Position = 0;
                    //Be sure to have a bin folder with lame dll files in there. 
                    //They also need to be loaded on application start up via Global.asax file
                    using (var mp3FileWriter = new LameMP3FileWriter
                    (outStream: mp3Stream, format: waveFormat, bitRate: bitRate))
                        synthWavMs.CopyTo(mp3FileWriter);
                }
            }
            catch (Exception ex)
            {
                Response.AddHeader("EXCEPTION", ex.GetBaseException().ToString());
            }
            finally
            {
                //Set no cache on this file
                Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();
                //required for chrome and safari
                Response.AppendHeader("Accept-Ranges", "bytes");
                //Write the byte length of mp3 to the client
                Response.AddHeader("Content-Length",
                    mp3Stream.Length.ToString(CultureInfo.InvariantCulture));
            }
            //return the converted wav to mp3 stream to a byte array for a file download
            return File(mp3Stream.ToArray(), "audio/mp3");
        }

        public ActionResult PlayTextArea(string text)
        { 
            return TextToMp3(text);
        }
        #endregion Read Story Content
    }
}
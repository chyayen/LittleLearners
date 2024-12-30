using ELearning.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace ELearning.Helpers
{
    public static class Utility
    {
        static string defaultConnection = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        public static List<StoryCategoryModel> GetStoryCategories()
        {
            List<StoryCategoryModel> list = new List<StoryCategoryModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT * FROM `storycategories` order by sequence";

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

        public static List<StoryModel> GetAllStoriesByStudent(int storycategoryid, int classID, int studentid)
        {
            List<StoryModel> list = new List<StoryModel>();
            MySqlConnection con = new MySqlConnection(defaultConnection);
            MySqlCommand cmd = con.CreateCommand();
            //cmd.CommandText = @"select c.*, u.name as addedbyname from stories c left join users u on c.addedby = u.id where (c.isdeleted is null or c.isdeleted = 0) and c.classid = @classid order by c.id desc;";
            cmd.CommandText = @"select distinct s.*, u.name as addedbyname, ssp.lastpageread, ssp.totalpages, ssp.status, ssp.allowretake, sc.id as storycategoryid
                                    , (select MAX(attempt) from studentanswers sa inner join storyquestions sq on sq.id = sa.questionid where sa.studentid = @studentid and sq.storyid = s.id) as attempt
                                from stories s 
                                inner join users u on s.addedby = u.id 
                                inner join storystudentassignments ssa on ssa.storyid = s.id
                                left join studentstoryprogress ssp on ssp.studentid = @studentid and ssp.storyid = s.id
                                left join storycategories sc on sc.code = ssp.status
                                where (s.isdeleted is null or s.isdeleted = 0) and s.classid = @classid 
	                                and ssa.sectionid in (select sectionid from studentclasses where studentid = @studentid)
                                    and (case when s.incomplete = 1 then 5 when sc.id is null then 1 else sc.id end) = @storycategoryid 
                                order by s.id desc;";
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
                        StudentID = studentid,
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
    }
}
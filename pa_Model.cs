using SMSPanasonic.Properties;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using Utils;

namespace SMSPanasonic.Business
{
    public partial class pa_Model
    {
        #region singleton pattern

        //private static List<pa_Model> _instance;
        private static object _lock = new object();
        //public static List<pa_Model> Instance
        //{
        //    get
        //    {
        //        lock (_lock)
        //        {
        //            if (_instance == null)
        //                _instance = ObjectFactory.CreateListFromProc<pa_Model>("usp_pa_Model_GetAll", new SqlParameter("@Disable", false));
        //            return _instance;
        //        }
        //    }
        //}

        //public static void ReloadData()
        //{
        //    lock (_lock)
        //    {
        //        _instance = ObjectFactory.CreateListFromProc<pa_Model>("usp_pa_Model_GetAll", new SqlParameter("@Disable", false));
        //    }
        //}

        #endregion singleton pattern

        #region public properties
        public string ModelCode { get; set; }
        public string CateCode { get; set; }
        public int WarrantyPeriod { get; set; }
        public string PeriodType { get; set; }
        public string Description { get; set; }
        public bool? ApprovedRequired { get; set; }
        public int? ApprovedDuration { get; set; }
        public bool? Disabled { get; set; }
        public bool UseTemplate { get; set; }
        public string TemplateName { get; set; }
        public int? ExpiredWarrantyDuration { get; set; }
        public int? ExpiredWarrantyValue { get; set; }
        public string Description2 { get; set; }
        public string Description3 { get; set; } //Bảo hành cho linh kiện
        public DateTime CreatedDate { get; set; }
        public bool? OutWarranty { get; set; }
        #endregion

        #region constructor
        public pa_Model() { }

        public pa_Model(IDataReader rd)
        {
            this.ModelCode = rd["ModelCode"].ToString();
            this.CateCode = rd["CateCode"].ToString();
            this.WarrantyPeriod = (int)rd["WarrantyPeriod"];
            this.PeriodType = rd["PeriodType"].ToString();
            this.Description = rd["Description"].ToString();
            this.ApprovedRequired = rd["ApprovedRequired"].Equals(DBNull.Value) ? (bool?)null : (bool)rd["ApprovedRequired"];
            this.ApprovedDuration = rd["ApprovedDuration"].Equals(DBNull.Value) ? (int?)null : (int)rd["ApprovedDuration"];
            this.Disabled = rd["Disabled"].Equals(DBNull.Value) ? (bool?)null : (bool)rd["Disabled"];
            this.UseTemplate = (bool)rd["UseTemplate"];
            this.TemplateName = rd["TemplateName"].Equals(DBNull.Value) ? null : (string)rd["TemplateName"];
            this.ExpiredWarrantyDuration = rd["ExpiredWarrantyDuration"].Equals(DBNull.Value) ? (int?)null : (int)rd["ExpiredWarrantyDuration"];
            this.ExpiredWarrantyValue = rd["ExpiredWarrantyValue"].Equals(DBNull.Value) ? (int?)null : (int)rd["ExpiredWarrantyValue"];
        }
        #endregion

        #region static method
        public static pa_Model GetOne(string modelCode)
        {
            ////return ObjectFactory.CreateInstanceFromProc<pa_Model>("usp_pa_Model_GetOne", new SqlParameter("@ModelCode", modelCode));
            //var model = Instance.Find(p => p.ModelCode.Equals(modelCode, StringComparison.OrdinalIgnoreCase));
            //if (model == null)
            //{
            //    var retvalue = ObjectFactory.CreateInstanceFromProc<pa_Model>("usp_pa_Model_GetOne", new SqlParameter("@ModelCode", modelCode));
            //    if (retvalue != null)
            //        Instance.Add(retvalue);
            //    return retvalue;
            //}
            //return model;
            return ObjectFactory.CreateInstanceFromProc<pa_Model>("usp_pa_Model_GetOne", new SqlParameter("@ModelCode", modelCode));
        }

        public static bool CheckExists(string modelCode)
        {
            return GetOne(modelCode) != null;
        }

        public static DateTime GetExpiredDate(pa_Model objModel, DateTime registerDate, DateTime? production_date)
        {
            try
            {
                DateTime expiredDate = registerDate.AddMonths(objModel.WarrantyPeriod).GetLastDayOfMonth();
                if (production_date != null) // Nếu có ngày sx (được bóc tách từ số máy)
                {
                    DateTime production_expired_max = (objModel.ExpiredWarrantyValue != null ? production_date.Value.AddMonths(objModel.ExpiredWarrantyValue.Value) : production_date.Value.AddMonths(objModel.WarrantyPeriod * 2)).GetLastDayOfMonth();
                    if (expiredDate > production_expired_max)
                    {
                        expiredDate = production_expired_max;
                    }
                }
                return expiredDate;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// Nghiệp vụ phát sinh: Nếu số máy tính được ngày sản xuất thì cần kiểm tra ngày hết hạn 
        /// có quá quy định từ ngày xuất xưởng hay không
        /// </summary>
        /// <param name="model_code">Model Code dau vao</param>
        /// <param name="product_code"> Product Code dau vao</param>
        /// <param name="mt_message">Message tra ra gui cho khach hang</param>
        /// <param name="production_date">Ngay san xuat cua san pham, neu khong tinh duoc thi tra ve NULL</param>
        /// <returns>True False</returns>
        public static bool CheckModelAndProduct(string model_code, string product_code, 
            ref string mt_message, ref DateTime? production_date)
        {
            try
            {
                string productReturnMsg = "";
                if (pa_Model.ValidProductCode(product_code, ref productReturnMsg))
                {
                    pa_Model obj_model = GetOne(model_code);
                    if (obj_model != null && obj_model.Disabled != true)
                    {
                        if (obj_model.OutWarranty == true)
                        {
                            mt_message = MessagePattern.GetMessage(eMessagePattern.MODELHETBAOHANH)
                                .Replace("(Model)", obj_model.ModelCode)
                                .Replace("(SoMay)", product_code);
                            return false;
                        }
                        else if (obj_model.UseTemplate && !string.IsNullOrEmpty(obj_model.TemplateName))
                        {
                            //Nếu model sử dụng template, tức là sản phẩm có thể đọc được ngày sản xuất từ số máy
                            //Trong trường hợp này, cần tính ra ngày sản xuất của số máy
                            //Nếu không tính ra được, gửi lại thông báo số máy không hợp lệ mới model
                            pa_SerialTemplate obj_template = pa_SerialTemplate.GetOne(obj_model.TemplateName);
                            if (obj_template == null)
                            {
                                mt_message = MessagePattern.GetMessage(eMessagePattern.MODELSOMAYKHONGHOPLE);
                                return false;
                            }
                            else
                            {
                                production_date = obj_template.GetDateTime(product_code);
                                if (production_date == null) //Không tính được ngày sản xuất của sản phẩm
                                {
                                    mt_message = MessagePattern.GetMessage(eMessagePattern.SOMAYKHONGHOPLE);
                                    return false;
                                }
                                else
                                {
                                    //Nếu ngày đăng ký sản phẩm lớn hơn hạn bảo hành tối đa
                                    //(Ví dụ ngày đăng ký đã quá 36 tháng từ ngày sản xuất)
                                    //Thì thông báo sản phẩm không đủ điểu kiện đăng ký bảo hành
                                    if (DateTime.Today >= production_date.Value.AddMonths(obj_model.ExpiredWarrantyValue.Value).Date.GetLastDayOfMonth())
                                    {
                                        //Nếu sản phẩm đã được bảo hành tồn kho và chưa hết hạn bảo hành
                                        var objGHTK = pa_WarrantyDetail.GetOne(model_code, product_code, eWarrantyType.GHTK);
                                        if (objGHTK != null && objGHTK.ExpiredDate.Date > DateTime.Today)
                                        {
                                            mt_message = string.Empty;
                                            return true;
                                        }
                                        else
                                        {
                                            mt_message = MessagePattern.GetMessage(eMessagePattern.SANPHAMKHONGDUDKBAOHANH)
                                                            .Replace("(Model)", obj_model.ModelCode)
                                                            .Replace("(SoMay)", product_code);
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        mt_message = string.Empty;
                                        return true;
                                    }
                                }
                            }
                        }
                        else // Nếu không đọc ngày sx từ số máy
                        {

                            //23/07/2018: cập nhật nghiệp vụ
                            //Nếu không tìm thấy ngày sản xuất trong bảng pa_ValidProduct thì return false
                            //25/10/2018: cập nhật nghiệp vụ
                            //Nếu không tìm thấy ngày sản xuất trong bảng pa_ValidProduct thì vẫn return true
                            production_date = pa_ValidProduct.GetProductionDate(model_code, product_code);
                            //if (production_date != null)
                            //{
                            //    mt_message = string.Empty;
                            //    return true;
                            //}
                            //else
                            //{
                            //    mt_message = MessagePattern.GetMessage(eMessagePattern.MODELSOMAYKHONGHOPLE);
                            //    return false;
                            //}

                            // START MODIFY BY TUANDQ 2019-05-23
                            // Kiem tra them truong hop neu co ngay san xuat va het han bao hanh thi thong bao san pham khong du DK bao hanh
                            //Nếu ngày đăng ký sản phẩm lớn hơn hạn bảo hành tối đa
                            //(Ví dụ ngày đăng ký đã quá 36 tháng từ ngày sản xuất)
                            //Thì thông báo sản phẩm không đủ điểu kiện đăng ký bảo hành
                            if (production_date != null && 
                                DateTime.Today >= production_date.Value.AddMonths(obj_model.ExpiredWarrantyValue.Value).Date.GetLastDayOfMonth())
                            {
                                //Nếu sản phẩm đã được bảo hành tồn kho và chưa hết hạn bảo hành
                                var objGHTK = pa_WarrantyDetail.GetOne(model_code, product_code, eWarrantyType.GHTK);
                                if (objGHTK != null && objGHTK.ExpiredDate.Date > DateTime.Today)
                                {
                                    mt_message = string.Empty;
                                    return true;
                                }
                                else
                                {
                                    mt_message = MessagePattern.GetMessage(eMessagePattern.SANPHAMKHONGDUDKBAOHANH)
                                                    .Replace("(Model)", obj_model.ModelCode)
                                                    .Replace("(SoMay)", product_code);
                                    return false;
                                }
                            }
                            else
                            {
                                mt_message = string.Empty;
                                return true;
                            }
                            // END MODIFY BY TUANDQ 2019-05-23
                            
                        }
                    }
                    else
                    {
                        mt_message = MessagePattern.GetMessage(eMessagePattern.MODELKHONGTONTAI);
                        return false;
                    }
                }
                else
                {
                    //mt_message = MessagePattern.GetMessage(eMessagePattern.SOMAYKHONGHOPLE);
                    mt_message = productReturnMsg.ToVietnameseWithoutAccent();
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Common.WriteLog(ex, "application_error");
                // START UPDATE BY TUANDQ 2019/06/18
                // Sua lai cach ghi log, ghi nhu dong tren rat kho tim, trong khi tra ve cho khach hang case: MODELSOMAYKHONGHOPLE
                Common.WriteLog(ex, "model");
                // END UPDATE BY TUANDQ 2019/06/18
                mt_message = eMessagePattern.MODELSOMAYKHONGHOPLE;
                return false;
            }
        }

        /// <summary>
        /// Kiem tra Ma san pham co hop le khong
        /// </summary>
        /// <param name="productCode">Ma san pham can kiem tra</param>
        /// <param name="returnMessage">Message tra ra, neu ma san pham hop khong hop le, returnMessage se co gia tr</param>
        /// <returns>True: Ma san pham hop le; False: Ma san pham khong hop le</returns>
        public static bool ValidProductCode(string productCode, ref string returnMessage)
        {
            bool rs = true;
            if (!Regex.IsMatch(productCode, Settings.Default.ProductCodeRegexPattern))
            {
                rs = false;
                returnMessage = Settings.Default.ProductCodeRegexMessage;
            }
            return rs;
        }
        #endregion
    }
}

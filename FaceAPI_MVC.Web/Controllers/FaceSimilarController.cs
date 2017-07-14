using FaceAPI_MVC.Web.Helper;
using FaceAPI_MVC.Web.Models;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FaceAPI_MVC.Web.Controllers
{
    public class FaceSimilarController : Controller
    {
        private static string ServiceKey = ConfigurationManager.AppSettings["FaceServiceKey"];
        private static string directory = "../UploadedFiles";
        private static string _faceListName = string.Empty;
        private static ObservableCollection<vmFace> _facesCollection = new ObservableCollection<vmFace>();
        private static ObservableCollection<vmFindSimilarResult> _findSimilarCollection = new ObservableCollection<vmFindSimilarResult>();

        public ObservableCollection<vmFace> FacesCollection
        {
            get
            {
                return _facesCollection;
            }
        }
        public ObservableCollection<vmFindSimilarResult> FindSimilarCollection
        {
            get
            {
                return _findSimilarCollection;
            }
        }


        // GET: FaceSimilar
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> SaveCandidateFiles()
        {
            string message = string.Empty, fileName = string.Empty, actualFileName = string.Empty; bool flag = false;

            //Requested File Collection
            HttpFileCollection fileRequested = System.Web.HttpContext.Current.Request.Files;
            if (fileRequested != null)
            {
                //Create New Folder
                CreateDirectory();

                //Clear Existing File in Folder
                ClearDirectory();

                for (int i = 0; i < fileRequested.Count; i++)
                {
                    var file = Request.Files[i];
                    actualFileName = file.FileName;
                    fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    int size = file.ContentLength;
                    string FullImgPath = Path.Combine(Server.MapPath(directory), fileName);
                    try
                    {
                        file.SaveAs(FullImgPath);
                        message = "File uploaded successfully";
                        flag = true;

                        if (FullImgPath != "")
                        {
                            using (var fStream = System.IO.File.OpenRead(FullImgPath))
                            {
                                // User picked one image
                                var imageInfo = UIHelper.GetImageInfoForRendering(FullImgPath);

                                // Create Instance of Service Client by passing Servicekey as parameter in constructor 
                                var faceServiceClient = new FaceServiceClient(ServiceKey);
                                Face[] faces = await faceServiceClient.DetectAsync(fStream, true, true, new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Glasses });

                                if (faces.Count() > 0)
                                {
                                    Bitmap CroppedFace = null;
                                    foreach (var face in faces)
                                    {
                                        //Create & Save Cropped Images
                                        var croppedImg = Convert.ToString(Guid.NewGuid()) + ".jpeg" as string;
                                        var croppedImgPath = directory + '\\' + croppedImg as string;
                                        var croppedImgFullPath = Server.MapPath(directory) + '\\' + croppedImg as string;
                                        CroppedFace = CropBitmap(
                                                        (Bitmap)Image.FromFile(FullImgPath),
                                                        face.FaceRectangle.Left,
                                                        face.FaceRectangle.Top,
                                                        face.FaceRectangle.Width,
                                                        face.FaceRectangle.Height);
                                        CroppedFace.Save(croppedImgFullPath, ImageFormat.Jpeg);
                                        if (CroppedFace != null)
                                            ((IDisposable)CroppedFace).Dispose();
                                    }

                                    //Clear Query File
                                    DeleteFile(FullImgPath);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        message = "File upload failed! Please try again";
                    }
                }
            }
            return new JsonResult
            {
                Data = new
                {
                    Message = message,
                    Status = flag
                }
            };
        }

        [HttpGet]
        public async Task<dynamic> GetCandidateFiles()
        {
            string message = string.Empty;
            var faceServiceClient = new FaceServiceClient(ServiceKey);
            FacesCollection.Clear();
            DirectoryInfo dir = new DirectoryInfo(Path.Combine(Server.MapPath(directory)));
            FileInfo[] files = null;
            files = dir.GetFiles().OrderBy(p => p.CreationTime).ToArray();

            if (files.Count() > 0)
            {
                _faceListName = Guid.NewGuid().ToString();
                await faceServiceClient.CreateFaceListAsync(_faceListName, _faceListName, "face list for sample");

                foreach (var item in files)
                {
                    var imgPath = Server.MapPath(directory) + '\\' + item.Name as string;
                    try
                    {
                        using (var fStream = System.IO.File.OpenRead(imgPath))
                        {
                            var faces = await faceServiceClient.AddFaceToFaceListAsync(_faceListName, fStream);
                            FacesCollection.Add(new vmFace
                            {
                                ImagePath = imgPath,
                                FileName = item.Name,
                                FilePath = directory + '\\' + item.Name,
                                FaceId = Convert.ToString(faces.PersistedFaceId)
                            });
                        }
                    }
                    catch (FaceAPIException fe)
                    {
                        //do exception work
                        message = fe.ToString();
                    }
                }

            }
            else
            {
                message = "No files to Detect!! Please Upload Files";
            }
            return new JsonResult
            {
                Data = new
                {
                    Message = message,
                    FacesCollection = FacesCollection
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpPost]
        public async Task<dynamic> FindSimilar()
        {
            string message = string.Empty, fileName = string.Empty, actualFileName = string.Empty; bool flag = false;
            var faceServiceClient = new FaceServiceClient(ServiceKey);
            FindSimilarCollection.Clear();

            //Requested File Collection
            HttpFileCollection fileRequested = System.Web.HttpContext.Current.Request.Files;

            if (fileRequested != null)
            {
                for (int i = 0; i < fileRequested.Count; i++)
                {
                    var file = Request.Files[i];
                    actualFileName = file.FileName;
                    fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    int size = file.ContentLength;

                    try
                    {
                        file.SaveAs(Path.Combine(Server.MapPath(directory), fileName));
                        var imgPath = Server.MapPath(directory) + '/' + fileName as string;
                        using (var fStream = System.IO.File.OpenRead(imgPath))
                        {
                            var faces = await faceServiceClient.DetectAsync(fStream);

                            //Find similar faces for each face
                            foreach (var f in faces)
                            {
                                var faceId = f.FaceId;
                                try
                                {
                                    //Call find similar REST API, the result contains all the face ids which similar to the query face
                                    const int requestCandidatesCount = 10;
                                    var result = await faceServiceClient.FindSimilarAsync(faceId, _faceListName, requestCandidatesCount);

                                    var findResult = new vmFindSimilarResult();
                                    findResult.Faces = new ObservableCollection<vmFace>();
                                    findResult.QueryFace = new vmFace()
                                    {
                                        ImagePath = imgPath,
                                        FileName = fileName,
                                        FilePath = directory + '/' + fileName,
                                        Top = f.FaceRectangle.Top,
                                        Left = f.FaceRectangle.Left,
                                        Width = f.FaceRectangle.Width,
                                        Height = f.FaceRectangle.Height,
                                        FaceId = faceId.ToString(),
                                    };

                                    //Update find similar results collection for rendering
                                    foreach (var fr in result)
                                    {
                                        findResult.Faces.Add(FacesCollection.First(ff => ff.FaceId == fr.PersistedFaceId.ToString()));
                                    }

                                    //Update UI
                                    FindSimilarCollection.Add(findResult);
                                    message = Convert.ToString("Total " + findResult.Faces.Count() + " faces are detected.");
                                    flag = true;
                                }
                                catch (FaceAPIException fex)
                                {
                                    message = fex.ErrorMessage;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.ToString();
                    }
                }
            }
            return new JsonResult
            {
                Data = new
                {
                    Message = message,
                    SimilarFace = FindSimilarCollection,
                    Status = flag
                }
            };
        }

        public Bitmap CropBitmap(Bitmap bitmap, int cropX, int cropY, int cropWidth, int cropHeight)
        {
            Rectangle rect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
            Bitmap cropped = bitmap.Clone(rect, bitmap.PixelFormat);
            return cropped;
        }

        public void CreateDirectory()
        {
            bool exists = System.IO.Directory.Exists(Server.MapPath(directory));
            if (!exists)
            {
                try
                {
                    Directory.CreateDirectory(Server.MapPath(directory));
                }
                catch (Exception ex)
                {
                    ex.ToString();
                }
            }
        }

        public void ClearDirectory()
        {
            DirectoryInfo dir = new DirectoryInfo(Path.Combine(Server.MapPath(directory)));
            var files = dir.GetFiles();
            if (files.Length > 0)
            {
                try
                {
                    foreach (FileInfo fi in dir.GetFiles())
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        fi.Delete();
                    }
                }
                catch (Exception ex)
                {
                    ex.ToString();
                }
            }
        }

        public void DeleteFile(string FullImgPath)
        {
            if (FullImgPath != "")
            {
                try
                {
                    //Clear Query File
                    if ((System.IO.File.Exists(FullImgPath)))
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        System.IO.File.Delete(FullImgPath);
                    }
                }
                catch (Exception ex)
                {
                    ex.ToString();
                }
            }
        }
    }
}
﻿using Dicom.Network;
using FUJI.SenderFeed2SCU.Service.DataBase;
using FUJI.SenderFeed2SCU.Service.Entidades;
using FUJI.SenderFeed2SCU.Service.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace FUJI.SenderFeed2SCU.Service
{
    partial class SenderFeed2SCUService : ServiceBase
    {
        private System.Timers.Timer SenderTimer = new System.Timers.Timer();
        public static clsConfiguracion _conf;
        public static int id_Servicio = 0;
        public static string vchClaveSitio = "";
        public static NAPOLEONEntities NapoleonDA = new NAPOLEONEntities();
        public static string AETitle = "";
        public static string vchPathRep = "";
        public static string path = "";

        public SenderFeed2SCUService()
        {
            //cargarServicio();
            cargarTimmer();
        }

        private void cargarServicio()
        {
            throw new NotImplementedException();
        }

        private void cargarTimmer()
        {
            try
            {
                try
                {
                    path = ConfigurationManager.AppSettings["ConfigDirectory"] != null ? ConfigurationManager.AppSettings["ConfigDirectory"].ToString() : "";
                }
                catch (Exception ePath)
                {
                    path = "";
                    Log.EscribeLog("Error al obtener el path desde appSettings: " + ePath.Message);
                }
                if (File.Exists(path + "info.xml"))
                {
                    _conf = XMLConfigurator.getXMLfile();
                    id_Servicio = _conf.id_Sitio;
                    AETitle = _conf.vchAETitle;
                    vchPathRep = _conf.vchPathLocal;
                    vchClaveSitio = _conf.vchClaveSitio;
                }
                Console.WriteLine("Se cargó correctamente el servicio SenderFeed2SCUService. " + "[" + DateTime.Now.ToShortDateString() + DateTime.Now.ToShortTimeString() + "]");
                Log.EscribeLog("Se cargó correctamente el servicio SenderFeed2SCUService. ");
                int minutosPoleo = ConfigurationManager.AppSettings["minutosPoleo"] != null ? Convert.ToInt32(ConfigurationManager.AppSettings["minutosPoleo"].ToString()) : 1;
                int minutos = 1000 * 60 * minutosPoleo;
                SenderTimer.Elapsed += new System.Timers.ElapsedEventHandler(SenderTimer_Elapsed);
                SenderTimer.Interval = minutos;
                SenderTimer.Enabled = true;
                SenderTimer.Start();
                try
                {
                    NapoleonSenderDataAccess.setService(id_Servicio, vchClaveSitio);
                }
                catch(Exception setSer)
                {
                    Log.EscribeLog("Existe un error en setService: "+ setSer.Message);
                }
            }
            catch(Exception eCS)
            {
                Log.EscribeLog("Existe un error en cargarServicio: " + eCS.Message);
            }
        }

        private void SenderTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                try
                {
                    NapoleonSenderDataAccess.setService(id_Servicio, vchClaveSitio);
                }
                catch (Exception setSer)
                {
                    Log.EscribeLog("Existe un error en setService: " + setSer.Message);
                }
                Log.EscribeLog("[" + DateTime.Now.ToShortDateString() + DateTime.Now.ToShortTimeString() + "] Leyendo estudios para Enviar.");
                Console.WriteLine("[" + DateTime.Now.ToShortDateString() + DateTime.Now.ToShortTimeString() + "] Leyendo estudios para Enviar.");

                NapoleonSenderDataAccess NapServer = new NapoleonSenderDataAccess();
                List<clsEstudio> _lstEst = new List<clsEstudio>();
                _lstEst = NapServer.getEstudiosEnviar(id_Servicio);
                if(_lstEst!= null)
                {
                    if(_lstEst.Count > 0 )
                    {
                        foreach(clsEstudio estudio in _lstEst)
                        {
                            //enviar uno por uni
                            //verificar Folder
                            if(File.Exists(estudio.vchPathFile))
                            {
                                Thread.Sleep(3000);
                                sendFile(estudio.vchPathFile, estudio.intDetEstudioID);
                            }
                            else
                            {
                                Log.EscribeLog("No existe el archivo " + estudio.vchPathFile + "  para ser enviado");
                            }
                            //si existe enviar
                        }
                    }
                }
            }
            catch(Exception eSyn)
            {
                Log.EscribeLog("Existe un error en SenderTimer_Elapsed: " + eSyn.Message);
            }
        }

        private Task<string> sendFile(string fullpath, int intDetEstudioID)
        {
            return Task.Run(() =>
            {
                string respuesta = "";
                try
                {
                    try
                    {
                        
                        string _ser = _conf.vchIPServidor;
                        int port = _conf.intPuertoServer;
                        string _aetS = _conf.vchAETitle;
                        string _aetA = _conf.vchAETitleServer;

                        if (_ser != "" && port > 0 && _aetA != "" && _aetS != "")
                        {
                            var client = new DicomClient();
                            client.NegotiateAsyncOps();
                            client.AddRequest(new DicomCEchoRequest());
                            client.AddRequest(new DicomCStoreRequest(fullpath));
                            Log.EscribeLog("IP Servidor destino: " + _ser);
                            Log.EscribeLog("Puerto Servidor destino: " + port.ToString());
                            Log.EscribeLog("AETitle Local: " + _aetS);
                            Log.EscribeLog("AETitle Server: " + _aetA);
                            client.Send(_ser, port, false, _aetS, _aetA);
                            respuesta = "1";

                            Log.EscribeLog("Enviado: " + fullpath);
                        }
                        else
                        {
                            Log.EscribeLog("Los parámetros para el envío no estan completos, favor de verificar: ");
                            Log.EscribeLog("IP Servidor destino: " + _ser);
                            Log.EscribeLog("Puerto Servidor destino: " + port.ToString());
                            Log.EscribeLog("AETitle Local: " + _aetS);
                            Log.EscribeLog("AETitle Server: " + _aetA);
                            respuesta = "0";
                        }
                        try
                        {
                            NapoleonSenderDataAccess.setService(id_Servicio, vchClaveSitio);
                        }
                        catch(Exception es)
                        {
                            Log.EscribeLog("Existe un error en enviar el estatus del servicio: " + es.Message);
                        }
                    }
                    catch (Exception eENVIar)
                    {
                        respuesta = "0";
                        Log.EscribeLog("Existe un error al enviar el archivo:" + eENVIar.Message);
                        Console.WriteLine("Error al enviar el Estudio:" + eENVIar.Message);
                    }
                    if (respuesta == "1")
                    {
                        NapoleonSenderDataAccess NapServer = new NapoleonSenderDataAccess();
                        NapServer.updateEstatus(intDetEstudioID);
                        //moverFile(fullpath, respuesta);
                    }
                    
                }
                catch (Exception esf)
                {
                    respuesta = "0";
                    Log.EscribeLog("Error: " + esf.Message);
                }
                return respuesta;
            });
        }

        private void moverFile(string fullpath, string Correcto)
        {
            try
            {
                string pathDest = "";
                pathDest = fullpath.Replace(vchPathRep, "");
                string filename = Path.GetFileName(pathDest);
                pathDest = pathDest.Replace(filename, "");

                if (!Directory.Exists(path + @"Exito\" + pathDest))
                    Directory.CreateDirectory(path + @"Exito\" + pathDest);
                if (!Directory.Exists(path + @"Error\" + pathDest))
                    Directory.CreateDirectory(path + @"Error\" + pathDest);

                if (Correcto == "" || Correcto == "1")
                {
                    Log.EscribeLog("Archivo " + Path.GetFileName(fullpath) + " correcto");
                    File.Move(fullpath, path + @"Exito\" + pathDest + Path.GetFileName(fullpath));
                }
                //else
                //{
                //    Log.EscribeLog("Archivo " + Path.GetFileName(fullpath) + " con errores.");
                //    File.Move(fullpath, path + @"Error\" + pathDest + Path.GetFileName(fullpath));
                //}
            }
            catch (Exception eMF)
            {
                Log.EscribeLog("Existe un error al mover el archivo: " + eMF.Message);
            }
        }

        protected override void OnStart(string[] args)
        {
            // TODO: agregar código aquí para iniciar el servicio.
        }

        protected override void OnStop()
        {
            // TODO: agregar código aquí para realizar cualquier anulación necesaria para detener el servicio.
        }
    }
}

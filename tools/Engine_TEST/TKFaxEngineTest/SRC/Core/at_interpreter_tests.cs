/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * at_interpreter_tests.cs - complete AT interpreter command tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004, 2005, 2006 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2, as
 * published by the Free Software Foundation.
 *
 * Managed C# port. All 320 command/response cases from the supplied
 * native spanDSP test are retained. Seven identity checks and six
 * trie/chaining/parser-position cases extend the separate test project.
 */

#nullable enable
using TKFaxEngine;

using System.Text;

namespace TKFaxEngineTest.Core.Test;

public static class at_interpreter_tests {
    private const byte DLE = 0x10;
    private const byte ETX = 0x03;
    private const byte SUB = 0x1A;
    private const string MANUFACTURER = "www.soft-switch.org";
    private const string PACKAGE = "TKFaxEngine";
    private const string VERSION = "0.0.6";

    private static readonly CommandResponse[] GeneralTestSequence =
    [
        new("atE1\r", "atE1\r\r\nOK\r\n"),
        new("AtE1\r", "AtE1\r\r\nOK\r\n"),
        new("aTE1\r", "aTE1\r\r\nOK\r\n"),
        new("ATE1\r", "ATE1\r\r\nOK\r\n"),
        new("ATE0\r", "ATE0\r\r\nOK\r\n"),
        new("ATS8?\r", "\r\n005\r\n\r\nOK\r\n"),
        new("ATS8=1\r", "\r\nOK\r\n"),
        new("ATS8.5?\r", "\r\n0\r\n\r\nOK\r\n"),
        new("ATS8.5=1\r", "\r\nOK\r\n"),
        new("ATS8.5?\r", "\r\n1\r\n\r\nOK\r\n"),
        new("ATS8?\r", "\r\n033\r\n\r\nOK\r\n"),
        new("AT+FCLASS=1\r", "\r\nOK\r\n"),
        new("AT+FCLASS?\r", "\r\n1\r\n\r\nOK\r\n"),
        new("AT+FCLASS=?\r", "\r\n0,1,1.0\r\n\r\nOK\r\n"),
        new("AT&C\r", "\r\nOK\r\n"),
        new("AT&D\r", "\r\nOK\r\n"),
        new("AT&F\r", "\r\nOK\r\n"),
        new("ATE0\r", "ATE0\r\r\nOK\r\n"),
        new("AT+A8E=?\r", "\r\n+A8E:(0-6),(0-5),(00-FF)\r\n\r\nOK\r\n"),
        new("AT+A8M\r", "\r\nOK\r\n"),
        new("AT+A8T=?\r", "\r\n+A8T:(0-10)\r\n\r\nOK\r\n"),
        new("AT+ASTO=?\r", "\r\n+ASTO:\r\n\r\nOK\r\n"),
        new("AT+CAAP=?\r", "\r\n+CAAP:\r\n\r\nOK\r\n"),
        new("AT+CACM=?\r", "\r\n+CACM:\r\n\r\nOK\r\n"),
        new("AT+CACSP=?\r", "\r\n+CACSP:\r\n\r\nOK\r\n"),
        new("AT+CAEMLPP=?\r", "\r\n+CAEMLPP:\r\n\r\nOK\r\n"),
        new("AT+CAHLD=?\r", "\r\n+CAHLD:\r\n\r\nOK\r\n"),
        new("AT+CAJOIN=?\r", "\r\n+CAJOIN:\r\n\r\nOK\r\n"),
        new("AT+CALA=?\r", "\r\n+CALA:\r\n\r\nOK\r\n"),
        new("AT+CALCC=?\r", "\r\n+CALCC:\r\n\r\nOK\r\n"),
        new("AT+CALD=?\r", "\r\n+CALD:\r\n\r\nOK\r\n"),
        new("AT+CALM=?\r", "\r\n+CALM:\r\n\r\nOK\r\n"),
        new("AT+CAMM=?\r", "\r\n+CAMM:\r\n\r\nOK\r\n"),
        new("AT+CANCHEV=?\r", "\r\n+CANCHEV:\r\n\r\nOK\r\n"),
        new("AT+CAOC=?\r", "\r\n+CAOC:\r\n\r\nOK\r\n"),
        new("AT+CAPD=?\r", "\r\n+CAPD:\r\n\r\nOK\r\n"),
        new("AT+CAPTT=?\r", "\r\n+CAPTT:\r\n\r\nOK\r\n"),
        new("AT+CAREJ=?\r", "\r\n+CAREJ:\r\n\r\nOK\r\n"),
        new("AT+CAULEV=?\r", "\r\n+CAULEV:\r\n\r\nOK\r\n"),
        new("AT+CBC=?\r", "\r\n+CBC:\r\n\r\nOK\r\n"),
        new("AT+CBCS=?\r", "\r\n+CBCS:\r\n\r\nOK\r\n"),
        new("AT+CBST=?\r", "\r\n+CBST:\r\n\r\nOK\r\n"),
        new("AT+CCFC=?\r", "\r\n+CCFC:\r\n\r\nOK\r\n"),
        new("AT+CCLK=?\r", "\r\n+CCLK:\r\n\r\nOK\r\n"),
        new("AT+CCUG=?\r", "\r\n+CCUG:\r\n\r\nOK\r\n"),
        new("AT+CCWA=?\r", "\r\n+CCWA:\r\n\r\nOK\r\n"),
        new("AT+CCWE=?\r", "\r\n+CCWE:\r\n\r\nOK\r\n"),
        new("AT+CDIP=?\r", "\r\n+CDIP:\r\n\r\nOK\r\n"),
        new("AT+CDIS=?\r", "\r\n+CDIS:\r\n\r\nOK\r\n"),
        new("AT+CEER=?\r", "\r\n+CEER:\r\n\r\nOK\r\n"),
        new("AT+CFCS=?\r", "\r\n+CFCS:\r\n\r\nOK\r\n"),
        new("AT+CFUN=?\r", "\r\n+CFUN:\r\n\r\nOK\r\n"),
        new("AT+CGACT=?\r", "\r\n+CGACT:\r\n\r\nOK\r\n"),
        new("AT+CGANS=?\r", "\r\n+CGANS:\r\n\r\nOK\r\n"),
        new("AT+CGATT=?\r", "\r\n+CGATT:\r\n\r\nOK\r\n"),
        new("AT+CGAUTO=?\r", "\r\n+CGAUTO:\r\n\r\nOK\r\n"),
        new("AT+CGCLASS=?\r", "\r\n+CGCLASS:\r\n\r\nOK\r\n"),
        new("AT+CGCLOSP=?\r", "\r\n+CGCLOSP:\r\n\r\nOK\r\n"),
        new("AT+CGCLPAD=?\r", "\r\n+CGCLPAD:\r\n\r\nOK\r\n"),
        new("AT+CGCMOD=?\r", "\r\n+CGCMOD:\r\n\r\nOK\r\n"),
        new("AT+CGCS=?\r", "\r\n+CGCS:\r\n\r\nOK\r\n"),
        new("AT+CGDATA=?\r", "\r\n+CGDATA:\r\n\r\nOK\r\n"),
        new("AT+CGDCONT=?\r", "\r\n+CGDCONT:\r\n\r\nOK\r\n"),
        new("AT+CGDSCONT=?\r", "\r\n+CGDSCONT:\r\n\r\nOK\r\n"),
        new("AT+CGEQMIN=?\r", "\r\n+CGEQMIN:\r\n\r\nOK\r\n"),
        new("AT+CGEQNEG=?\r", "\r\n+CGEQNEG:\r\n\r\nOK\r\n"),
        new("AT+CGEQREQ=?\r", "\r\n+CGEQREQ:\r\n\r\nOK\r\n"),
        new("AT+CGEREP=?\r", "\r\n+CGEREP:\r\n\r\nOK\r\n"),
        new("AT+CGMI=?\r", "\r\n+CGMI:\r\n\r\nOK\r\n"),
        new("AT+CGMM=?\r", "\r\n+CGMM:\r\n\r\nOK\r\n"),
        new("AT+CGMR=?\r", "\r\n+CGMR:\r\n\r\nOK\r\n"),
        new("AT+CGPADDR=?\r", "\r\n+CGPADDR:\r\n\r\nOK\r\n"),
        new("AT+CGQMIN=?\r", "\r\n+CGQMIN:\r\n\r\nOK\r\n"),
        new("AT+CGQREQ=?\r", "\r\n+CGQREQ:\r\n\r\nOK\r\n"),
        new("AT+CGREG=?\r", "\r\n+CGREG:\r\n\r\nOK\r\n"),
        new("AT+CGSMS=?\r", "\r\n+CGSMS:\r\n\r\nOK\r\n"),
        new("AT+CGSN=?\r", "\r\n+CGSN:\r\n\r\nOK\r\n"),
        new("AT+CGTFT=?\r", "\r\n+CGTFT:\r\n\r\nOK\r\n"),
        new("AT+CHLD=?\r", "\r\n+CHLD:\r\n\r\nOK\r\n"),
        new("AT+CHSA=?\r", "\r\n+CHSA:\r\n\r\nOK\r\n"),
        new("AT+CHSC=?\r", "\r\n+CHSC:\r\n\r\nOK\r\n"),
        new("AT+CHSD=?\r", "\r\n+CHSD:\r\n\r\nOK\r\n"),
        new("AT+CHSN=?\r", "\r\n+CHSN:\r\n\r\nOK\r\n"),
        new("AT+CHSR=?\r", "\r\n+CHSR:\r\n\r\nOK\r\n"),
        new("AT+CHST=?\r", "\r\n+CHST:\r\n\r\nOK\r\n"),
        new("AT+CHSU=?\r", "\r\n+CHSU:\r\n\r\nOK\r\n"),
        new("AT+CHUP=?\r", "\r\n+CHUP:\r\n\r\nOK\r\n"),
        new("AT+CIMI=?\r", "\r\n+CIMI:\r\n\r\nOK\r\n"),
        new("AT+CIND=?\r", "\r\n+CIND:\r\n\r\nOK\r\n"),
        new("AT+CKPD=?\r", "\r\n+CKPD:\r\n\r\nOK\r\n"),
        new("AT+CLAC=?\r", "\r\n+CLAC:\r\n\r\nOK\r\n"),
        new("AT+CLAE=?\r", "\r\n+CLAE:\r\n\r\nOK\r\n"),
        new("AT+CLAN=?\r", "\r\n+CLAN:\r\n\r\nOK\r\n"),
        new("AT+CLCC=?\r", "\r\n+CLCC:\r\n\r\nOK\r\n"),
        new("AT+CLCK=?\r", "\r\n+CLCK:\r\n\r\nOK\r\n"),
        new("AT+CLIP=?\r", "\r\n+CLIP:\r\n\r\nOK\r\n"),
        new("AT+CLIR=?\r", "\r\n+CLIR:\r\n\r\nOK\r\n"),
        new("AT+CLVL=?\r", "\r\n+CLVL:\r\n\r\nOK\r\n"),
        new("AT+CMAR=?\r", "\r\n+CMAR:\r\n\r\nOK\r\n"),
        new("AT+CMEC=?\r", "\r\n+CMEC:\r\n\r\nOK\r\n"),
        new("AT+CMER=?\r", "\r\n+CMER:\r\n\r\nOK\r\n"),
        new("AT+CMOD=?\r", "\r\n+CMOD:\r\n\r\nOK\r\n"),
        new("AT+CMUT=?\r", "\r\n+CMUT:\r\n\r\nOK\r\n"),
        new("AT+CMUX=?\r", "\r\n+CMUX:\r\n\r\nOK\r\n"),
        new("AT+CNUM=?\r", "\r\n+CNUM:\r\n\r\nOK\r\n"),
        new("AT+COLP=?\r", "\r\n+COLP:\r\n\r\nOK\r\n"),
        new("AT+COPN=?\r", "\r\n+COPN:\r\n\r\nOK\r\n"),
        new("AT+COPS=?\r", "\r\n+COPS:\r\n\r\nOK\r\n"),
        new("AT+COTDI=?\r", "\r\n+COTDI:\r\n\r\nOK\r\n"),
        new("AT+CPAS=?\r", "\r\n+CPAS:\r\n\r\nOK\r\n"),
        new("AT+CPBF=?\r", "\r\n+CPBF:\r\n\r\nOK\r\n"),
        new("AT+CPBR=?\r", "\r\n+CPBR:\r\n\r\nOK\r\n"),
        new("AT+CPBS=?\r", "\r\n+CPBS:\r\n\r\nOK\r\n"),
        new("AT+CPBW=?\r", "\r\n+CPBW:\r\n\r\nOK\r\n"),
        new("AT+CPIN=?\r", "\r\n+CPIN:\r\n\r\nOK\r\n"),
        new("AT+CPLS=?\r", "\r\n+CPLS:\r\n\r\nOK\r\n"),
        new("AT+CPOL=?\r", "\r\n+CPOL:\r\n\r\nOK\r\n"),
        new("AT+CPPS=?\r", "\r\n+CPPS:\r\n\r\nOK\r\n"),
        new("AT+CPROT=?\r", "\r\n+CPROT:\r\n\r\nOK\r\n"),
        new("AT+CPUC=?\r", "\r\n+CPUC:\r\n\r\nOK\r\n"),
        new("AT+CPWC=?\r", "\r\n+CPWC:\r\n\r\nOK\r\n"),
        new("AT+CPWD=?\r", "\r\n+CPWD:\r\n\r\nOK\r\n"),
        new("AT+CR=?\r", "\r\n+CR:\r\n\r\nOK\r\n"),
        new("AT+CRC=?\r", "\r\n+CRC:\r\n\r\nOK\r\n"),
        new("AT+CREG=?\r", "\r\n+CREG:\r\n\r\nOK\r\n"),
        new("AT+CRLP=?\r", "\r\n+CRLP:\r\n\r\nOK\r\n"),
        new("AT+CRMC=?\r", "\r\n+CRMC:\r\n\r\nOK\r\n"),
        new("AT+CRMP=?\r", "\r\n+CRMP:\r\n\r\nOK\r\n"),
        new("AT+CRSL=?\r", "\r\n+CRSL:\r\n\r\nOK\r\n"),
        new("AT+CRSM=?\r", "\r\n+CRSM:\r\n\r\nOK\r\n"),
        new("AT+CSCC=?\r", "\r\n+CSCC:\r\n\r\nOK\r\n"),
        new("AT+CSCS=?\r", "\r\n+CSCS:\r\n\r\nOK\r\n"),
        new("AT+CSDF=?\r", "\r\n+CSDF:\r\n\r\nOK\r\n"),
        new("AT+CSGT=?\r", "\r\n+CSGT:\r\n\r\nOK\r\n"),
        new("AT+CSIL=?\r", "\r\n+CSIL:\r\n\r\nOK\r\n"),
        new("AT+CSIM=?\r", "\r\n+CSIM:\r\n\r\nOK\r\n"),
        new("AT+CSNS=?\r", "\r\n+CSNS:\r\n\r\nOK\r\n"),
        new("AT+CSQ=?\r", "\r\n+CSQ:\r\n\r\nOK\r\n"),
        new("AT+CSSN=?\r", "\r\n+CSSN:\r\n\r\nOK\r\n"),
        new("AT+CSTA=?\r", "\r\n+CSTA:\r\n\r\nOK\r\n"),
        new("AT+CSTF=?\r", "\r\n+CSTF:\r\n\r\nOK\r\n"),
        new("AT+CSVM=?\r", "\r\n+CSVM:\r\n\r\nOK\r\n"),
        new("AT+CTFR=?\r", "\r\n+CTFR:\r\n\r\nOK\r\n"),
        new("AT+CTZR=?\r", "\r\n+CTZR:\r\n\r\nOK\r\n"),
        new("AT+CTZU=?\r", "\r\n+CTZU:\r\n\r\nOK\r\n"),
        new("AT+CUSD=?\r", "\r\n+CUSD:\r\n\r\nOK\r\n"),
        new("AT+CUUS1=?\r", "\r\n+CUUS1:\r\n\r\nOK\r\n"),
        new("AT+CV120=?\r", "\r\n+CV120:\r\n\r\nOK\r\n"),
        new("AT+CVHU=?\r", "\r\n+CVHU:\r\n\r\nOK\r\n"),
        new("AT+CVIB=?\r", "\r\n+CVIB:\r\n\r\nOK\r\n"),
        new("AT+DR=?\r", "\r\n+DR:\r\n\r\nOK\r\n"),
        new("AT+DS=?\r", "\r\n+DS:\r\n\r\nOK\r\n"),
        new("AT+EB=?\r", "\r\n+EB:\r\n\r\nOK\r\n"),
        new("AT+EFCS=?\r", "\r\n+EFCS:(0-2)\r\n\r\nOK\r\n"),
        new("AT+EFCS?\r", "\r\n+EFCS:0\r\n\r\nOK\r\n"),
        new("AT+EFRAM=?\r", "\r\n+EFRAM:(1-65535),(1-65535)\r\n\r\nOK\r\n"),
        new("AT+ER=?\r", "\r\n+ER:(0,1)\r\n\r\nOK\r\n"),
        new("AT+ES=?\r", "\r\n+ES:(0-7),(0-4),(0-9)\r\n\r\nOK\r\n"),
        new("AT+ES?\r", "\r\n+ES:0,0,0\r\n\r\nOK\r\n"),
        new("AT+ESA=?\r", "\r\n+ESA:(0-2),(0-1),(0-1),(0-1),(0-2),(0-1),(0-255),(0-255)\r\n\r\nOK\r\n"),
        new("AT+ESA?\r", "\r\n+ESA:0,0,0,0,0,0,0,0\r\n\r\nOK\r\n"),
        new("AT+ESR\r", "\r\nOK\r\n"),
        new("AT+ETBM=?\r", "\r\n+ETBM:(0-2),(0-2),(0-30)\r\n\r\nOK\r\n"),
        new("AT+ETBM?\r", "\r\n+ETBM:0,0\r\n\r\nOK\r\n"),
        new("AT+EWIND=?\r", "\r\n+EWIND:(1-127),(1-127)\r\n\r\nOK\r\n"),
        new("AT+EWIND?\r", "\r\n+EWIND:0,0\r\n\r\nOK\r\n"),
        new("AT+F34=?\r", "\r\n+F34:(0-14),(0-14),(0-2),(0-14),(0-14)\r\n\r\nOK\r\n"),
        new("AT+F34?\r", "\r\n+F34:0,0,0,0,0\r\n\r\nOK\r\n"),
        new("AT+FAR=?\r", "\r\n0,1\r\n\r\nOK\r\n"),
        new("AT+FAR?\r", "\r\n0\r\n\r\nOK\r\n"),
        new("AT+FCL=?\r", "\r\n(0-255)\r\n\r\nOK\r\n"),
        new("AT+FCLASS=?\r", "\r\n0,1,1.0\r\n\r\nOK\r\n"),
        new("AT+FCLASS?\r", "\r\n1\r\n\r\nOK\r\n"),
        new("AT+FDD=?\r", "\r\n(0,1)\r\n\r\nOK\r\n"),
        new("AT+FDD?\r", "\r\n0\r\n\r\nOK\r\n"),
        new("AT+FIT=?\r", "\r\n+FIT:(0-255),(0-1)\r\n\r\nOK\r\n"),
        new("AT+FIT?\r", "\r\n+FIT:0,0\r\n\r\nOK\r\n"),
        new("AT+FLO=?\r", "\r\n+FLO:(0-2)\r\n\r\nOK\r\n"),
        new("AT+FLO?\r", "\r\n+FLO:2\r\n\r\nOK\r\n"),
        new("AT+FMI?\r", "\r\nwww.soft-switch.org\r\n\r\nOK\r\n"),
        new("AT+FMM?\r", "\r\nTKFaxEngine\r\n\r\nOK\r\n"),
        new("AT+FMR?\r", "\r\n0.0.6\r\n\r\nOK\r\n"),
        new("AT+FPR=?\r", "\r\n115200\r\n\r\nOK\r\n"),
        new("AT+FPR?\r", "\r\n0\r\n\r\nOK\r\n"),
        new("AT+FRH=?\r", "\r\n3\r\n\r\nOK\r\n"),
        new("AT+FRH?\r", "\r\n-1\r\n\r\nOK\r\n"),
        new("AT+FRM=?\r", "\r\n24,48,72,73,74,96,97,98,121,122,145,146\r\n\r\nOK\r\n"),
        new("AT+FRM?\r", "\r\n-1\r\n\r\nOK\r\n"),
        new("AT+FRS=?\r", "\r\n0-255\r\n\r\nOK\r\n"),
        new("AT+FRS?\r", "\r\n-1\r\n\r\nOK\r\n"),
        new("AT+FTH=?\r", "\r\n3\r\n\r\nOK\r\n"),
        new("AT+FTH?\r", "\r\n-1\r\n\r\nOK\r\n"),
        new("AT+FTM=?\r", "\r\n24,48,72,73,74,96,97,98,121,122,145,146\r\n\r\nOK\r\n"),
        new("AT+FTM?\r", "\r\n-1\r\n\r\nOK\r\n"),
        new("AT+FTS=?\r", "\r\n0-255\r\n\r\nOK\r\n"),
        new("AT+FTS?\r", "\r\n-1\r\n\r\nOK\r\n"),
        new("AT+GCAP\r", "\r\nOK\r\n"),
        new("AT+GCI=?\r", "\r\n+GCI:(00-FF)\r\n\r\nOK\r\n"),
        new("AT+GCI?\r", "\r\n+GCI:00\r\n\r\nOK\r\n"),
        new("AT+GMI?\r", "\r\nwww.soft-switch.org\r\n\r\nOK\r\n"),
        new("AT+GMM?\r", "\r\nTKFaxEngine\r\n\r\nOK\r\n"),
        new("AT+GMR?\r", "\r\n0.0.6\r\n\r\nOK\r\n"),
        new("AT+GOI\r", "\r\nOK\r\n"),
        new("AT+GSN?\r", "\r\n42\r\n\r\nOK\r\n"),
        new("AT+IBC=?\r", "\r\n+IBC:(0-2),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0.1),(0,1)\r\n\r\nOK\r\n"),
        new("AT+IBC?\r", "\r\n+IBC:0,0,0,0,0,0,0,0,0,0,0,0,0\r\n\r\nOK\r\n"),
        new("AT+IBM=?\r", "\r\n+IBM:(0-7),(0-255),(0-255)\r\n\r\nOK\r\n"),
        new("AT+IBM?\r", "\r\n+IBM:0,0,0\r\n\r\nOK\r\n"),
        new("AT+ICF?\r", "\r\n+ICF:0,0\r\n\r\nOK\r\n"),
        new("AT+ICLOK?\r", "\r\n+ICLOK:0\r\n\r\nOK\r\n"),
        new("AT+IDSR?\r", "\r\n+IDSR:0\r\n\r\nOK\r\n"),
        new("AT+IFC=?\r", "\r\n+IFC:(0-2),(0-2)\r\n\r\nOK\r\n"),
        new("AT+IFC?\r", "\r\n+IFC:2,2\r\n\r\nOK\r\n"),
        new("AT+ILRR\r", "\r\nOK\r\n"),
        new("AT+ILSD=?\r", "\r\n+ILSD:(0,1)\r\n\r\nOK\r\n"),
        new("AT+ILSD?\r", "\r\n+ILSD:0\r\n\r\nOK\r\n"),
        new("AT+IPR=?\r", "\r\n+IPR:(115200),(115200)\r\n\r\nOK\r\n"),
        new("AT+IPR?\r", "\r\n+IPR:0\r\n\r\nOK\r\n"),
        new("AT+IRTS=?\r", "\r\n+IRTS:(0,1)\r\n\r\nOK\r\n"),
        new("AT+IRTS?\r", "\r\n+IRTS:0\r\n\r\nOK\r\n"),
        new("AT+MA\r", "\r\nOK\r\n"),
        new("AT+MR=?\r", "\r\n+MR:(0,1)\r\n\r\nOK\r\n"),
        new("AT+MR?\r", "\r\n+MR:0\r\n\r\nOK\r\n"),
        new("AT+MS\r", "\r\nOK\r\n"),
        new("AT+MSC=?\r", "\r\n+MSC:(0,1)\r\n\r\nOK\r\n"),
        new("AT+MSC?\r", "\r\n+MSC:0\r\n\r\nOK\r\n"),
        new("AT+MV18AM\r", "\r\nOK\r\n"),
        new("AT+MV18P=?\r", "\r\n+MV18P:(2-7)\r\n\r\nOK\r\n"),
        new("AT+MV18P?\r", "\r\n+MV18P:0\r\n\r\nOK\r\n"),
        new("AT+MV18R=?\r", "\r\n+MV18R:(0,1)\r\n\r\nOK\r\n"),
        new("AT+MV18R?\r", "\r\n+MV18R:0\r\n\r\nOK\r\n"),
        new("AT+MV18S\r", "\r\nOK\r\n"),
        new("AT+TADR\r", "\r\nOK\r\n"),
        new("AT+TAL=?\r", "\r\n+TAL:(0,1),(0,1)\r\n\r\nOK\r\n"),
        new("AT+TAL?\r", "\r\n+TAL:0,0\r\n\r\nOK\r\n"),
        new("AT+TALS=?\r", "\r\n+TALS:(0-3)\r\n\r\nOK\r\n"),
        new("AT+TALS?\r", "\r\n+TALS:0\r\n\r\nOK\r\n"),
        new("AT+TDLS=?\r", "\r\n+TDLS:(0-4)\r\n\r\nOK\r\n"),
        new("AT+TDLS?\r", "\r\n+TDLS:0\r\n\r\nOK\r\n"),
        new("AT+TE140=?\r", "\r\n+TE140:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TE140?\r", "\r\n+TE140:0\r\n\r\nOK\r\n"),
        new("AT+TE141=?\r", "\r\n+TE141:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TE141?\r", "\r\n+TE141:0\r\n\r\nOK\r\n"),
        new("AT+TEPAL=?\r", "\r\n+TEPAL:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TEPAL?\r", "\r\n+TEPAL:0\r\n\r\nOK\r\n"),
        new("AT+TEPDL=?\r", "\r\n+TEPDL:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TEPDL?\r", "\r\n+TEPDL:0\r\n\r\nOK\r\n"),
        new("AT+TERDL=?\r", "\r\n+TERDL:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TERDL?\r", "\r\n+TERDL:0\r\n\r\nOK\r\n"),
        new("AT+TLDL=?\r", "\r\n+TLDL:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TLDL?\r", "\r\n+TLDL:0\r\n\r\nOK\r\n"),
        new("AT+TMODE=?\r", "\r\n+TMODE:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TMODE?\r", "\r\n+TMODE:0\r\n\r\nOK\r\n"),
        new("AT+TNUM\r", "\r\nOK\r\n"),
        new("AT+TRDL=?\r", "\r\n+TRDL:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TRDL?\r", "\r\n+TRDL:0\r\n\r\nOK\r\n"),
        new("AT+TRDLS\r", "\r\nOK\r\n"),
        new("AT+TRES=?\r", "\r\n+TRES:(0-2)\r\n\r\nOK\r\n"),
        new("AT+TRES?\r", "\r\n+TRES:0\r\n\r\nOK\r\n"),
        new("AT+TSELF=?\r", "\r\n+TSELF:(0,1)\r\n\r\nOK\r\n"),
        new("AT+TSELF?\r", "\r\n+TSELF:0\r\n\r\nOK\r\n"),
        new("AT+TTER=?\r", "\r\n+TTER:(0-65535),(0-65535)\r\n\r\nOK\r\n"),
        new("AT+TTER?\r", "\r\n+TTER:0,0\r\n\r\nOK\r\n"),
        new("AT+VBT\r", "\r\nOK\r\n"),
        new("AT+VCID=?\r", "\r\n0,1\r\n\r\nOK\r\n"),
        new("AT+VCID?\r", "\r\n0\r\n\r\nOK\r\n"),
        new("AT+VDR\r", "\r\nOK\r\n"),
        new("AT+VDT\r", "\r\nOK\r\n"),
        new("AT+VDX\r", "\r\nOK\r\n"),
        new("AT+VEM\r", "\r\nOK\r\n"),
        new("AT+VGM\r", "\r\nOK\r\n"),
        new("AT+VGR\r", "\r\nOK\r\n"),
        new("AT+VGS\r", "\r\nOK\r\n"),
        new("AT+VGT\r", "\r\nOK\r\n"),
        new("AT+VIP\r", "\r\nOK\r\n"),
        new("AT+VIT\r", "\r\nOK\r\n"),
        new("AT+VLS\r", "\r\nOK\r\n"),
        new("AT+VPP\r", "\r\nOK\r\n"),
        new("AT+VRA\r", "\r\nOK\r\n"),
        new("AT+VRID?\r", "\r\n0\r\n\r\nOK\r\n"),
        new("AT+VRL\r", "\r\nOK\r\n"),
        new("AT+VRN\r", "\r\nOK\r\n"),
        new("AT+VRX\r", "\r\nOK\r\n"),
        new("AT+VSD\r", "\r\nOK\r\n"),
        new("AT+VSID=12345\r", "\r\nOK\r\n"),
        new("AT+VSID?\r", "\r\n12345\r\n\r\nOK\r\n"),
        new("AT+VSM\r", "\r\nOK\r\n"),
        new("AT+VSP\r", "\r\nOK\r\n"),
        new("AT+VTA\r", "\r\nOK\r\n"),
        new("AT+VTD\r", "\r\nOK\r\n"),
        new("AT+VTH\r", "\r\nOK\r\n"),
        new("AT+VTR\r", "\r\nOK\r\n"),
        new("AT+VTS\r", "\r\nOK\r\n"),
        new("AT+VTX\r", "\r\nOK\r\n"),
        new("AT+WS46\r", "\r\nOK\r\n"),
        new("ATA\r", "\r\nERROR\r\n"),
        new("ATDT -1234567890ABCDPSTW*#+,!@\r;", ""),
        new("ATE1\r", "\r\nOK\r\n"),
        new("ATE0\r", "ATE0\r\r\nOK\r\n"),
        new("ATH\r", "\r\nOK\r\n"),
        new("ATI\r", "\r\nTKFaxEngine\r\n\r\nOK\r\n"),
        new("ATL\r", "\r\nOK\r\n"),
        new("ATM\r", "\r\nOK\r\n"),
        new("ATO\r", "\r\nCONNECT\r\n\r\nOK\r\n"),
        new("ATP\r", "\r\nOK\r\n"),
        new("ATQ\r", "\r\nOK\r\n"),
        new("ATS0=?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS0?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS10=?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS10?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS3=?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS3?\r", "\r\n013\r\n\r\nOK\r\n"),
        new("ATS4=?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS4?\r", "\r\n010\r\n\r\nOK\r\n"),
        new("ATS5=?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS5?\r", "\r\n008\r\n\r\nOK\r\n"),
        new("ATS6=?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS6?\r", "\r\n001\r\n\r\nOK\r\n"),
        new("ATS7=?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS7?\r", "\r\n060\r\n\r\nOK\r\n"),
        new("ATS8=?\r", "\r\n000\r\n\r\nOK\r\n"),
        new("ATS8?\r", "\r\n005\r\n\r\nOK\r\n"),
        new("ATT\r", "\r\nOK\r\n"),
        new("ATV0\r", "0\r"),
        new("ATV1\r", "\r\nOK\r\n"),
        new("ATX4\r", "\r\nOK\r\n"),
        new("ATZ\r", "\r\nOK\r\n"),
        new("", ""),
    ];

    private static readonly CommandResponse[] TrieDispatchTestSequence =
    [
        new("ATE0V1\r", "ATE0V1\r\r\nOK\r\n"),
        new("ATE1;V1\r", "\r\nOK\r\n"),
        new(
            "AT+ICF=3,1+ICF?\r",
            "AT+ICF=3,1+ICF?\r\r\n+ICF:3,1\r\n\r\nOK\r\n"),
        new(
            "ATS8=1S8?\r",
            "ATS8=1S8?\r\r\n001\r\n\r\nOK\r\n"),
        new("AT;E0;V1\r", "AT;E0;V1\r\r\nOK\r\n"),
        new("AT~\r", "\r\nOK\r\n"),
        new("", ""),
    ];

    private static string? decode_test_file = null;
    private static int countdown = 0;
    private static int command_response_test_step = -1;
    private static readonly byte[] response_buf = new byte[1000];
    private static int response_buf_ptr;

    public static int main(string[] testArguments) {
        try {
            return tkfx_at_interpreter_tests_body_main(testArguments);
        } catch (AtInterpreterTestExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_at_interpreter_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        _ = DLE;
        _ = ETX;
        _ = SUB;
        _ = MANUFACTURER;
        _ = PACKAGE;
        _ = VERSION;
        _ = decode_test_file;
        _ = countdown;

        AtInterpreterState state = new AtInterpreterState(
                AtTxHandler,
                null,
                ModemCallControl,
                null,
                PACKAGE,
                VERSION);

        try {
            if (GeneralTest(state) != 0) {
                Console.WriteLine("Tests failed.");
                Exit(2);
            }

            if (TrieDispatchTest() != 0) {
                Console.WriteLine("Trie dispatch tests failed.");
                Exit(2);
            }

            Console.WriteLine("Tests passed.");
            return 0;
        } finally {
            _ = AtInterpreterApi.at_free(state);
        }
    }

    private static int AtRx(AtInterpreterState state, string text, int length) {
        AtInterpreterApi.at_interpreter(state, text, length);
        return 0;
    }

    private static int AtSend(AtInterpreterState state, string text) {
        Console.Write(text);
        return AtRx(state, text, text.Length);
    }

    private static int GeneralTest(AtInterpreterState state) {
        for (int i = 0; GeneralTestSequence[i].Command.Length != 0; i++) {
            response_buf_ptr = 0;
            Array.Clear(response_buf);
            command_response_test_step = i;

            _ = AtSend(state, GeneralTestSequence[i].Command);

            string received = Encoding.ASCII.GetString(
                response_buf,
                0,
                response_buf_ptr);

            string expected =
                GeneralTestSequence[command_response_test_step].Response;

            if (!string.Equals(expected, received, StringComparison.Ordinal)) {
                Console.WriteLine("Incorrect response");
                Console.WriteLine($"Expected: '{expected}'");
                Console.WriteLine($"Received: '{received}'");
                return -1;
            }
        }

        return 0;
    }

    private static int TrieDispatchTest() {
        AtInterpreterState state = new(
            AtTxHandler,
            null,
            ModemCallControl,
            null,
            PACKAGE,
            VERSION);

        try {
            for (int i = 0; TrieDispatchTestSequence[i].Command.Length != 0; i++) {
                response_buf_ptr = 0;
                Array.Clear(response_buf);

                _ = AtSend(state, TrieDispatchTestSequence[i].Command);

                string received = Encoding.ASCII.GetString(
                    response_buf,
                    0,
                    response_buf_ptr);
                string expected = TrieDispatchTestSequence[i].Response;

                if (!string.Equals(expected, received, StringComparison.Ordinal)) {
                    Console.WriteLine("Incorrect trie dispatch response");
                    Console.WriteLine($"Expected: '{expected}'");
                    Console.WriteLine($"Received: '{received}'");
                    return -1;
                }
            }

            return 0;
        } finally {
            _ = AtInterpreterApi.at_free(state);
        }
    }

    private static int ModemCallControl(
        object? userData,
        int operation,
        string? number) {
        _ = userData;

        switch ((AtModemControlOperation)operation) {
            case AtModemControlOperation.Call:
                Console.WriteLine($"\nModem control - Dialing '{number ?? string.Empty}'");
                break;

            case AtModemControlOperation.Answer:
                Console.WriteLine("\nModem control - Answering");
                // Force the deterministic ERROR response used by the native test.
                return -1;

            case AtModemControlOperation.Hangup:
                Console.WriteLine("\nModem control - Hanging up");
                break;

            case AtModemControlOperation.OffHook:
                Console.WriteLine("\nModem control - Going off hook");
                break;

            case AtModemControlOperation.OnHook:
                Console.WriteLine("\nModem control - Going on hook");
                break;

            case AtModemControlOperation.Dtr:
                Console.WriteLine($"\nModem control - DTR {number ?? "0"}");
                break;

            case AtModemControlOperation.Rts:
                Console.WriteLine($"\nModem control - RTS {number ?? "0"}");
                break;

            case AtModemControlOperation.Cts:
                Console.WriteLine($"\nModem control - CTS {number ?? "0"}");
                break;

            case AtModemControlOperation.Carrier:
                Console.WriteLine($"\nModem control - CAR {number ?? "0"}");
                break;

            case AtModemControlOperation.Ring:
                Console.WriteLine($"\nModem control - RNG {number ?? "0"}");
                break;

            case AtModemControlOperation.Dsr:
                Console.WriteLine($"\nModem control - DSR {number ?? "0"}");
                break;

            case AtModemControlOperation.SetId:
                Console.WriteLine($"\nModem control - Set ID '{number ?? string.Empty}'");
                break;

            case AtModemControlOperation.Restart:
                Console.WriteLine($"\nModem control - Restart {number ?? "0"}");
                break;

            case AtModemControlOperation.DteTimeout:
                Console.WriteLine($"\nModem control - Set DTE timeout {number ?? "0"}");
                break;

            default:
                Console.WriteLine($"\nModem control - operation {operation}");
                break;
        }

        return 0;
    }

    private static int AtTxHandler(
        object? userData,
        ReadOnlySpan<byte> data) {
        _ = userData;

        if (response_buf_ptr + data.Length > response_buf.Length)
            throw new InvalidOperationException("The AT response buffer overflowed.");

        data.CopyTo(response_buf.AsSpan(response_buf_ptr));
        response_buf_ptr += data.Length;

        Console.Write(Encoding.ASCII.GetString(data));
        return 0;
    }

    private static void Exit(int code) {
        throw new AtInterpreterTestExitException(code);
    }

    private readonly record struct CommandResponse(
        string Command,
        string Response);

    public sealed class AtInterpreterTestExitException : Exception {
        public AtInterpreterTestExitException(int exitCode)
            : base($"at_interpreter_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}

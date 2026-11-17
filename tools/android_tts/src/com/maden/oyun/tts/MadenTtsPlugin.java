package com.maden.oyun.tts;

import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.content.pm.ServiceInfo;
import android.media.AudioAttributes;
import android.media.AudioManager;
import android.os.Bundle;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.Looper;
import android.provider.Settings;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;
import android.util.Log;

import org.godotengine.godot.Godot;
import org.godotengine.godot.plugin.GodotPlugin;
import org.godotengine.godot.plugin.UsedByGodot;
import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

/**
 * Maden Android TTS koprusu (Godot Android plugin v2).
 *
 * Godot'nun yerlesik TTS'i yalnizca varsayilan motoru kullanir; bu sinif kurulu HER metin okuma motorunu
 * (TextToSpeech.getEngines) listeler ve secilen motorla konusur.
 *
 * C# tarafi: Engine.GetSingleton("MadenTts").Call("Speak", ...). Karmasik donusler JSON metnidir.
 * Tum TTS cagrilari ayri bir is parcacigindadir (oyun iplugini bloke etmez). Motor hazir olmadan gelen
 * konusmalar kuyruga alinir ve hazir olunca sirayla soylenir.
 *
 * Ses akisi: USAGE_ASSISTANCE_ACCESSIBILITY (varsayilan). Gerekce: TalkBack ile ayni "erisilebilirlik" hacmi
 * kullanilir, yani konusma hacmi muzik/oyun (USAGE_MEDIA) hacminden bagimsizdir; medya sesi kisikken
 * konusma kisilmaz. SetAudioUsage("media") ile medya akisina gecilebilir (konusma sessiz kalirsa yedek).
 */
public class MadenTtsPlugin extends GodotPlugin {
    private static final String TAG = "MadenTts";
    public static final String NAME = "MadenTts";
    public static final int VERSION = 1;
    /** SetLanguage/SetVoice motor hazir degilken: istek saklandi, hazir olunca uygulanir. */
    public static final int PENDING = 100;
    private static final long INIT_TIMEOUT_MS = 10000;
    private static final int MAX_PENDING = 16;

    private static final class Pending {
        final String text; final double rate; final double pitch; final boolean interrupt; final String id;
        Pending(String text, double rate, double pitch, boolean interrupt, String id) {
            this.text = text; this.rate = rate; this.pitch = pitch; this.interrupt = interrupt; this.id = id;
        }
    }

    private final Object lock = new Object();
    private final Handler main = new Handler(Looper.getMainLooper());
    private final HandlerThread bgThread = new HandlerThread("MadenTts");
    private Handler bg;

    private TextToSpeech tts;
    private int gen = 0;                 // eski Init'lerin gec kalan geri cagrilarini yok saymak icin
    private String state = "none";       // none | initializing | ready | error
    private String requestedEngine = "";
    private long initStartMs = 0;
    private String lastError = "";
    private final ArrayList<Pending> pending = new ArrayList<>();
    private final Set<String> active = new HashSet<>();
    private long seq = 0;

    private String wantLang = "tr", wantCountry = "TR", wantVoice = "";
    private int langResult = PENDING;
    private int voiceResult = PENDING;
    private String audioUsage = "accessibility";

    public MadenTtsPlugin(Godot godot) {
        super(godot);
        bgThread.start();
        bg = new Handler(bgThread.getLooper());
    }

    @Override
    public String getPluginName() { return NAME; }

    private Context ctx() {
        Context c = getActivity();
        if (c == null) c = getGodot().getContext();
        return c == null ? null : c.getApplicationContext();
    }

    private void setError(String msg) {
        Log.w(TAG, msg);
        synchronized (lock) { lastError = msg; }
    }

    // ------------------------------------------------------------------ Surum / durum

    @UsedByGodot
    public int Version() { return VERSION; }

    @UsedByGodot
    public boolean IsReady() { synchronized (lock) { return "ready".equals(state); } }

    @UsedByGodot
    public boolean IsSpeaking() { synchronized (lock) { return !active.isEmpty() || !pending.isEmpty(); } }

    @UsedByGodot
    public String GetLastError() { synchronized (lock) { return lastError; } }

    @UsedByGodot
    public void ClearError() { synchronized (lock) { lastError = ""; } }

    /** Ses tanisi: medya (STREAM_MUSIC, oyun sesi) ve erisilebilirlik (konusma) akisi seviyeleri. TTS erisilebilirlik akisini kullanir; medya sesi 0 ise efektler/muzik duyulmaz. */
    @UsedByGodot
    public String GetAudioInfo() {
        JSONObject o = new JSONObject();
        try {
            Context c = ctx();
            AudioManager am = c == null ? null : (AudioManager) c.getSystemService(Context.AUDIO_SERVICE);
            if (am == null) { o.put("error", "AudioManager yok"); return o.toString(); }
            o.put("musicVol", am.getStreamVolume(AudioManager.STREAM_MUSIC));
            o.put("musicMax", am.getStreamMaxVolume(AudioManager.STREAM_MUSIC));
            o.put("musicMuted", am.isStreamMute(AudioManager.STREAM_MUSIC));
            o.put("a11yVol", am.getStreamVolume(10 /* STREAM_ACCESSIBILITY */));
            o.put("a11yMax", am.getStreamMaxVolume(10));
            o.put("ringerMode", am.getRingerMode());
            o.put("mode", am.getMode());
            o.put("musicActive", am.isMusicActive());
            o.put("micMute", am.isMicrophoneMute());
        } catch (Throwable t) { try { o.put("error", String.valueOf(t)); } catch (Throwable ignored) { } }
        return o.toString();
    }

    /** {"state","engine","elapsedMs","lastError","speaking","queued","audioUsage","langResult","voiceResult"} */
    @UsedByGodot
    public String GetStatus() {
        JSONObject o = new JSONObject();
        try {
            synchronized (lock) {
                o.put("state", state);
                o.put("engine", requestedEngine);
                o.put("elapsedMs", "initializing".equals(state) ? System.currentTimeMillis() - initStartMs : 0);
                o.put("lastError", lastError);
                o.put("speaking", !active.isEmpty());
                o.put("queued", pending.size());
                o.put("audioUsage", audioUsage);
                o.put("langResult", langResult);
                o.put("voiceResult", voiceResult);
            }
        } catch (Throwable t) { Log.w(TAG, "GetStatus", t); }
        return o.toString();
    }

    // ------------------------------------------------------------------ Motorlar

    /** [{"package","label","default"}]. Gerekli: manifestte TTS_SERVICE icin <queries> (Android 11+ paket gorunurlugu). */
    @UsedByGodot
    public String ListEngines() {
        JSONArray arr = new JSONArray();
        try {
            String def = GetDefaultEngine();
            TextToSpeech t;
            synchronized (lock) { t = "ready".equals(state) ? tts : null; }
            Set<String> seen = new HashSet<>();
            if (t != null) {
                try {
                    List<TextToSpeech.EngineInfo> engines = t.getEngines();
                    if (engines != null) for (TextToSpeech.EngineInfo e : engines) {
                        if (e == null || e.name == null || !seen.add(e.name)) continue;
                        arr.put(engineJson(e.name, e.label, e.name.equals(def)));
                    }
                } catch (Throwable th) { setError("getEngines hatasi: " + th); }
            }
            if (arr.length() == 0) { // henuz motor yokken: getEngines'in yaptigi sorgunun aynisi
                Context c = ctx();
                if (c != null) {
                    PackageManager pm = c.getPackageManager();
                    List<ResolveInfo> list = pm.queryIntentServices(new Intent(TextToSpeech.Engine.INTENT_ACTION_TTS_SERVICE), PackageManager.GET_META_DATA);
                    if (list != null) for (ResolveInfo ri : list) {
                        ServiceInfo si = ri.serviceInfo;
                        if (si == null || !seen.add(si.packageName)) continue;
                        CharSequence label = null;
                        try { label = ri.loadLabel(pm); } catch (Throwable ignored) { }
                        arr.put(engineJson(si.packageName, label == null ? si.packageName : label.toString(), si.packageName.equals(def)));
                    }
                }
            }
        } catch (Throwable th) { setError("ListEngines hatasi: " + th); }
        return arr.toString();
    }

    private JSONObject engineJson(String pkg, String label, boolean isDefault) throws Exception {
        JSONObject o = new JSONObject();
        o.put("package", pkg);
        o.put("label", label == null || label.isEmpty() ? pkg : label);
        o.put("default", isDefault);
        return o;
    }

    /** Telefonun varsayilan motorunun paket adi ("" = bilinmiyor). */
    @UsedByGodot
    public String GetDefaultEngine() {
        try {
            TextToSpeech t;
            synchronized (lock) { t = "ready".equals(state) ? tts : null; }
            if (t != null) { String d = t.getDefaultEngine(); if (d != null && !d.isEmpty()) return d; }
        } catch (Throwable ignored) { }
        try {
            Context c = ctx();
            if (c != null) { String d = Settings.Secure.getString(c.getContentResolver(), "tts_default_synth"); if (d != null) return d; }
        } catch (Throwable ignored) { }
        return "";
    }

    /** Belirli motorla yeni TextToSpeech baslatir ("" = varsayilan). Eskisi kapatilir. Asenkron: IsReady/GetStatus ile izle. */
    @UsedByGodot
    public boolean Init(String enginePackage) {
        final String engine = enginePackage == null ? "" : enginePackage.trim();
        final int myGen;
        TextToSpeech old;
        synchronized (lock) {
            myGen = ++gen;
            old = tts; tts = null;
            state = "initializing";
            requestedEngine = engine;
            initStartMs = System.currentTimeMillis();
            lastError = "";
            active.clear();
            langResult = PENDING; voiceResult = PENDING;
        }
        if (old != null) { final TextToSpeech o = old; bg.post(() -> { try { o.stop(); o.shutdown(); } catch (Throwable ignored) { } }); }
        final Context c = ctx();
        if (c == null) { failInit(myGen, "Baglam (context) yok"); return false; }
        main.post(() -> {
            synchronized (lock) { if (myGen != gen) return; }
            try {
                TextToSpeech.OnInitListener l = status -> bg.post(() -> onInit(myGen, status));
                TextToSpeech t = engine.isEmpty() ? new TextToSpeech(c, l) : new TextToSpeech(c, l, engine);
                synchronized (lock) { if (myGen == gen) tts = t; else { try { t.shutdown(); } catch (Throwable ignored) { } } }
            } catch (Throwable th) { failInit(myGen, "TextToSpeech olusturulamadi (" + engine + "): " + th); }
        });
        bg.postDelayed(() -> {
            boolean timedOut;
            synchronized (lock) { timedOut = myGen == gen && "initializing".equals(state); }
            if (timedOut) failInit(myGen, "Motor " + INIT_TIMEOUT_MS / 1000 + " sn icinde acilmadi: " + (engine.isEmpty() ? "(varsayilan)" : engine));
        }, INIT_TIMEOUT_MS);
        return true;
    }

    private void failInit(int myGen, String msg) {
        synchronized (lock) { if (myGen != gen) return; state = "error"; lastError = msg; pending.clear(); }
        Log.w(TAG, msg);
    }

    private void onInit(final int myGen, int status) {
        final TextToSpeech t;
        synchronized (lock) { if (myGen != gen) return; t = tts; }
        if (status != TextToSpeech.SUCCESS || t == null) {
            failInit(myGen, "Motor baslatilamadi: " + (requestedEngine.isEmpty() ? "(varsayilan)" : requestedEngine) + " durum=" + status);
            return;
        }
        try {
            applyAudioAttributes(t);
            t.setOnUtteranceProgressListener(new UtteranceProgressListener() {
                @Override public void onStart(String id) { }
                @Override public void onDone(String id) { synchronized (lock) { active.remove(id); } }
                @Override public void onStop(String id, boolean interrupted) { synchronized (lock) { active.remove(id); } }
                @Override public void onError(String id) { onError(id, -1); }
                @Override public void onError(String id, int code) {
                    synchronized (lock) { active.remove(id); }
                    setError("Konusma hatasi id=" + id + " kod=" + code);
                }
            });
            if (!wantLang.isEmpty()) applyLanguage(t);
            if (!wantVoice.isEmpty()) applyVoice(t);
        } catch (Throwable th) { setError("Motor ayari hatasi: " + th); }
        List<Pending> run;
        synchronized (lock) {
            if (myGen != gen) return;
            state = "ready";
            run = new ArrayList<>(pending);
            pending.clear();
        }
        for (Pending p : run) doSpeak(t, p);
    }

    // ------------------------------------------------------------------ Sesler / dil

    /** [{"name","locale","lang","country","quality","network","installed","tr"}] (secili motorun sesleri). */
    @UsedByGodot
    public String ListVoices() {
        JSONArray arr = new JSONArray();
        TextToSpeech t;
        synchronized (lock) { t = "ready".equals(state) ? tts : null; }
        if (t == null) return arr.toString();
        try {
            Set<Voice> voices = t.getVoices();
            if (voices != null) for (Voice v : voices) {
                if (v == null) continue;
                Locale loc = v.getLocale();
                String lang = loc == null ? "" : loc.getLanguage();
                JSONObject o = new JSONObject();
                o.put("name", v.getName());
                o.put("locale", loc == null ? "" : loc.toLanguageTag());
                o.put("lang", lang);
                o.put("country", loc == null ? "" : loc.getCountry());
                o.put("quality", v.getQuality());
                o.put("network", v.isNetworkConnectionRequired());
                Set<String> f = v.getFeatures();
                o.put("installed", f == null || !f.contains(TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED));
                o.put("tr", "tr".equalsIgnoreCase(lang) || "tur".equalsIgnoreCase(lang));
                arr.put(o);
            }
        } catch (Throwable th) { setError("getVoices hatasi: " + th); }
        return arr.toString();
    }

    /** Dil secer. Donus: TextToSpeech.LANG_* kodu (0/1/2 = kullanilabilir, -1 veri yok, -2 desteklenmiyor) ya da PENDING. */
    @UsedByGodot
    public int SetLanguage(String lang, String country) {
        TextToSpeech t;
        synchronized (lock) {
            wantLang = lang == null ? "" : lang;
            wantCountry = country == null ? "" : country;
            t = "ready".equals(state) ? tts : null;
        }
        if (t == null) return PENDING;
        return applyLanguage(t);
    }

    private int applyLanguage(TextToSpeech t) {
        int r;
        try {
            Locale loc;
            synchronized (lock) { loc = new Locale(wantLang, wantCountry); }
            r = t.setLanguage(loc);
        } catch (Throwable th) { setError("setLanguage hatasi: " + th); r = TextToSpeech.ERROR; }
        synchronized (lock) { langResult = r; }
        if (r < 0) setError("Dil desteklenmiyor/veri yok (" + wantLang + "-" + wantCountry + ") kod=" + r);
        return r;
    }

    /** Ses secer (ListVoices'taki ad). "" = secili dilin varsayilan sesi. Donus: 0 tamam, -2 bulunamadi, PENDING. */
    @UsedByGodot
    public int SetVoice(String name) {
        TextToSpeech t;
        synchronized (lock) {
            wantVoice = name == null ? "" : name;
            t = "ready".equals(state) ? tts : null;
        }
        if (t == null) return PENDING;
        if (wantVoice.isEmpty()) return applyLanguage(t);
        return applyVoice(t);
    }

    private int applyVoice(TextToSpeech t) {
        int r = -2;
        try {
            String name; synchronized (lock) { name = wantVoice; }
            Set<Voice> voices = t.getVoices();
            if (voices != null) for (Voice v : voices) {
                if (v != null && name.equals(v.getName())) { r = t.setVoice(v); break; }
            }
        } catch (Throwable th) { setError("setVoice hatasi: " + th); r = TextToSpeech.ERROR; }
        synchronized (lock) { voiceResult = r; }
        if (r != 0) setError("Ses secilemedi: '" + wantVoice + "' kod=" + r);
        return r;
    }

    // ------------------------------------------------------------------ Ses akisi

    /** "accessibility" (varsayilan) ya da "media". */
    @UsedByGodot
    public void SetAudioUsage(String usage) {
        TextToSpeech t;
        synchronized (lock) {
            audioUsage = "media".equalsIgnoreCase(usage) ? "media" : "accessibility";
            t = "ready".equals(state) ? tts : null;
        }
        if (t != null) applyAudioAttributes(t);
    }

    private void applyAudioAttributes(TextToSpeech t) {
        try {
            String u; synchronized (lock) { u = audioUsage; }
            int usage = "media".equals(u) ? AudioAttributes.USAGE_MEDIA : AudioAttributes.USAGE_ASSISTANCE_ACCESSIBILITY;
            t.setAudioAttributes(new AudioAttributes.Builder()
                    .setUsage(usage)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
                    .build());
        } catch (Throwable th) { setError("setAudioAttributes hatasi: " + th); }
    }

    // ------------------------------------------------------------------ Konusma

    /** Soyler; utteranceId dondurur. Motor hazir degilse kuyruga alinir (gerekirse varsayilan motor baslatilir). */
    @UsedByGodot
    public String Speak(String text, double rate, double pitch, boolean interrupt) {
        if (text == null || text.trim().isEmpty()) return "";
        final String id;
        boolean needInit = false;
        TextToSpeech t = null;
        synchronized (lock) {
            id = "maden-" + (++seq);
            if ("ready".equals(state)) t = tts;
            else {
                if (interrupt) pending.clear();
                if (pending.size() >= MAX_PENDING) pending.remove(0);
                pending.add(new Pending(text, rate, pitch, interrupt, id));
                needInit = "none".equals(state);
            }
        }
        if (t != null) {
            final TextToSpeech tt = t;
            final Pending p = new Pending(text, rate, pitch, interrupt, id);
            bg.post(() -> doSpeak(tt, p));
        } else if (needInit) Init("");
        return id;
    }

    private void doSpeak(TextToSpeech t, Pending p) {
        try {
            t.setSpeechRate((float) Math.max(0.1, Math.min(4.0, p.rate)));
            t.setPitch((float) Math.max(0.2, Math.min(2.0, p.pitch)));
            int max = 3900;
            try { max = Math.max(200, TextToSpeech.getMaxSpeechInputLength() - 100); } catch (Throwable ignored) { }
            String s = p.text;
            boolean first = true;
            int chunkNo = 0;
            while (!s.isEmpty()) {
                String part = s;
                if (s.length() > max) {
                    int cut = s.lastIndexOf(' ', max);
                    if (cut < max / 2) cut = max;
                    part = s.substring(0, cut);
                    s = s.substring(cut).trim();
                } else s = "";
                String id = chunkNo == 0 ? p.id : p.id + "." + chunkNo;
                chunkNo++;
                synchronized (lock) { if (first && p.interrupt) active.clear(); active.add(id); }
                int r = t.speak(part, (first && p.interrupt) ? TextToSpeech.QUEUE_FLUSH : TextToSpeech.QUEUE_ADD, new Bundle(), id);
                if (r != TextToSpeech.SUCCESS) {
                    synchronized (lock) { active.remove(id); }
                    setError("speak basarisiz kod=" + r + " id=" + id);
                }
                first = false;
            }
        } catch (Throwable th) { setError("speak hatasi: " + th); }
    }

    @UsedByGodot
    public void Stop() {
        TextToSpeech t;
        synchronized (lock) { pending.clear(); active.clear(); t = "ready".equals(state) ? tts : null; }
        if (t != null) bg.post(() -> { try { t.stop(); } catch (Throwable th) { setError("stop hatasi: " + th); } });
    }

    @Override
    public void onMainDestroy() {
        TextToSpeech t;
        synchronized (lock) { gen++; t = tts; tts = null; state = "none"; }
        try { if (t != null) { t.stop(); t.shutdown(); } } catch (Throwable ignored) { }
    }
}

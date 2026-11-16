package com.maden.oyun;

import android.content.Context;
import android.media.AudioManager;
import android.os.Bundle;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.accessibility.AccessibilityManager;

import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

import java.util.Locale;

/**
 * Unity etkinliği alt sınıfı. İki iş yapar:
 *  1) Dokunma (normal + TalkBack'in hover'a çevirdiği olaylar) ve çok parmaklı dokunuşları Unity'ye iletir.
 *  2) Ses tuşlarını yakalar (sistem ses panelini bastırır) ve Unity'ye iletir.
 *
 * Unity tarafı: "MadenBridge" GameObject'i, OnNativeTouch / OnVolumeKey / OnTouchExploration.
 */
public class MadenActivity extends UnityPlayerActivity {
    private static final String BRIDGE = "MadenBridge";
    private static final long MOVE_INTERVAL_MS = 40;

    private static MadenActivity sInstance;

    private AccessibilityManager accessibilityManager;
    private AccessibilityManager.TouchExplorationStateChangeListener touchListener;
    private volatile boolean touchExploration;
    private long lastMoveSentTime = -1;
    private long lastEventTime = -1;
    private int lastEventAction = -1;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        sInstance = this;
        setVolumeControlStream(AudioManager.STREAM_MUSIC);
        accessibilityManager = (AccessibilityManager) getSystemService(Context.ACCESSIBILITY_SERVICE);
        touchExploration = computeTouchExploration(this);
        touchListener = new AccessibilityManager.TouchExplorationStateChangeListener() {
            @Override
            public void onTouchExplorationStateChanged(boolean enabled) {
                touchExploration = computeTouchExploration(MadenActivity.this);
                notifyTouchExploration();
            }
        };
        if (accessibilityManager != null) {
            accessibilityManager.addTouchExplorationStateChangeListener(touchListener);
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        touchExploration = computeTouchExploration(this);
        notifyTouchExploration();
    }

    @Override
    protected void onDestroy() {
        if (accessibilityManager != null && touchListener != null) {
            accessibilityManager.removeTouchExplorationStateChangeListener(touchListener);
        }
        if (sInstance == this) sInstance = null;
        super.onDestroy();
    }

    // Geri tuşu yanlışlıkla oyunu kapatmasın: arka plana al.
    @Override
    public void onBackPressed() {
        moveTaskToBack(true);
    }

    // ---------- Yardımcılar (Unity de çağırabilir) ----------

    private static boolean computeTouchExploration(Context ctx) {
        AccessibilityManager am = (AccessibilityManager) ctx.getSystemService(Context.ACCESSIBILITY_SERVICE);
        return am != null && am.isEnabled() && am.isTouchExplorationEnabled();
    }

    /** Unity: new AndroidJavaClass("com.maden.oyun.MadenActivity").CallStatic<bool>("isTouchExplorationEnabled") */
    public static boolean isTouchExplorationEnabled() {
        MadenActivity a = sInstance;
        return a != null && computeTouchExploration(a);
    }

    public static boolean isTouchExplorationEnabled(Context ctx) {
        return ctx != null && computeTouchExploration(ctx);
    }

    private void notifyTouchExploration() {
        send("OnTouchExploration", touchExploration ? "1" : "0");
    }

    private static void send(String method, String message) {
        try {
            UnityPlayer.UnitySendMessage(BRIDGE, method, message);
        } catch (Throwable ignored) {
            // Unity henüz hazır değilse sessizce geç.
        }
    }

    // ---------- Dokunma ----------

    @Override
    public boolean dispatchTouchEvent(MotionEvent ev) {
        handleMotion(ev);
        if (touchExploration) {
            return true; // TalkBack açıkken olayı tüket
        }
        return super.dispatchTouchEvent(ev);
    }

    // TalkBack tek parmak dokunuşunu HOVER olaylarına çevirir; bunlar burada yakalanır.
    @Override
    public boolean dispatchGenericMotionEvent(MotionEvent ev) {
        if (isHover(ev)) {
            handleMotion(ev);
            if (touchExploration) return true;
        }
        return super.dispatchGenericMotionEvent(ev);
    }

    @Override
    public boolean onGenericMotionEvent(MotionEvent ev) {
        if (isHover(ev)) {
            handleMotion(ev); // aynı olay zaten işlendiyse yinelenmez
            if (touchExploration) return true;
        }
        return super.onGenericMotionEvent(ev);
    }

    private static boolean isHover(MotionEvent ev) {
        int a = ev.getActionMasked();
        return a == MotionEvent.ACTION_HOVER_ENTER
                || a == MotionEvent.ACTION_HOVER_MOVE
                || a == MotionEvent.ACTION_HOVER_EXIT;
    }

    private void handleMotion(MotionEvent ev) {
        int action = ev.getActionMasked();
        long t = ev.getEventTime();
        if (t == lastEventTime && action == lastEventAction) return; // yinelenen olay
        lastEventTime = t;
        lastEventAction = action;

        String phase;
        int count = ev.getPointerCount();
        switch (action) {
            case MotionEvent.ACTION_DOWN:
            case MotionEvent.ACTION_POINTER_DOWN:
                phase = "down";
                break;
            case MotionEvent.ACTION_MOVE:
                phase = "move";
                break;
            case MotionEvent.ACTION_POINTER_UP:
                phase = "move";
                count = Math.max(1, count - 1);
                break;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                phase = "up";
                count = 0;
                break;
            case MotionEvent.ACTION_HOVER_ENTER:
                phase = "down";
                count = 1;
                break;
            case MotionEvent.ACTION_HOVER_MOVE:
                phase = "move";
                count = 1;
                break;
            case MotionEvent.ACTION_HOVER_EXIT:
                phase = "up";
                count = 0;
                break;
            default:
                return;
        }

        if (action == MotionEvent.ACTION_MOVE || action == MotionEvent.ACTION_HOVER_MOVE) {
            if (lastMoveSentTime >= 0 && t - lastMoveSentTime < MOVE_INTERVAL_MS) return;
            lastMoveSentTime = t;
        }

        // Merkez (ağırlık merkezi), dp cinsinden.
        float sx = 0, sy = 0;
        int n = ev.getPointerCount();
        for (int i = 0; i < n; i++) {
            sx += ev.getX(i);
            sy += ev.getY(i);
        }
        float density = getResources().getDisplayMetrics().density;
        if (density <= 0f) density = 1f;
        float x = (sx / Math.max(1, n)) / density;
        float y = (sy / Math.max(1, n)) / density;

        send("OnNativeTouch", String.format(Locale.US, "%s;%d;%.1f;%.1f;%d", phase, count, x, y, t));
    }

    // ---------- Ses tuşları ----------

    @Override
    public boolean dispatchKeyEvent(KeyEvent ev) {
        int code = ev.getKeyCode();
        if (code == KeyEvent.KEYCODE_VOLUME_UP || code == KeyEvent.KEYCODE_VOLUME_DOWN) {
            String key = code == KeyEvent.KEYCODE_VOLUME_UP ? "UP" : "DOWN";
            if (ev.getAction() == KeyEvent.ACTION_DOWN) {
                int rc = ev.getRepeatCount();
                if (rc == 0) send("OnVolumeKey", key + "|1");
                else if (rc == 1) send("OnVolumeKey", key + "|2"); // uzun basış
            } else if (ev.getAction() == KeyEvent.ACTION_UP) {
                send("OnVolumeKey", key + "|0");
            }
            return true; // sistem ses panelini ve ses değişimini bastır
        }
        return super.dispatchKeyEvent(ev);
    }
}

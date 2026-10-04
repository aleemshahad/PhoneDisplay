package com.phonestream.player

import android.annotation.SuppressLint
import android.app.Activity
import android.graphics.Color
import android.os.Bundle
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.view.WindowManager
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast

class MainActivity : Activity() {

    private lateinit var root: LinearLayout
    private lateinit var input: EditText
    private lateinit var connectButton: Button
    private lateinit var webView: WebView

    private val prefs by lazy { getSharedPreferences(PREFS_NAME, MODE_PRIVATE) }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        buildUi()
    }

    override fun onResume() {
        super.onResume()
        if (::webView.isInitialized) {
            webView.onResume()
            webView.resumeTimers()
        }
    }

    override fun onPause() {
        if (::webView.isInitialized) {
            webView.onPause()
            webView.pauseTimers()
        }
        super.onPause()
    }

    override fun onDestroy() {
        if (::webView.isInitialized) {
            webView.loadUrl("about:blank")
            webView.destroy()
        }
        super.onDestroy()
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() {
        if (::webView.isInitialized && webView.visibility == View.VISIBLE) {
            showConnectScreen()
        } else {
            super.onBackPressed()
        }
    }

    private fun buildUi() {
        root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(Color.BLACK)
        }

        val title = TextView(this).apply {
            text = getString(R.string.app_name)
            setTextColor(Color.WHITE)
            textSize = 22f
            gravity = Gravity.CENTER
        }
        root.addView(title, lp(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, top = dp(56)))

        val subtitle = TextView(this).apply {
            text = getString(R.string.connect_prompt)
            setTextColor(Color.LTGRAY)
            textSize = 14f
            gravity = Gravity.CENTER
        }
        root.addView(subtitle, lp(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, top = dp(8), bottom = dp(8)))

        input = EditText(this).apply {
            setText(prefs.getString(KEY_HOST, "") ?: "")
            setHint(R.string.address_hint)
            setTextColor(Color.WHITE)
            setHintTextColor(Color.GRAY)
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_URI
            setSingleLine()
        }
        root.addView(
            input,
            lp(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, dp(24), 0, dp(24), 0)
        )

        connectButton = Button(this).apply {
            text = getString(R.string.connect_button)
        }
        connectButton.setOnClickListener { connect() }
        root.addView(
            connectButton,
            lp(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, dp(24), dp(16), dp(24), 0)
        )

        webView = buildWebView()
        webView.visibility = View.GONE
        root.addView(
            webView,
            LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0).apply { weight = 1f }
        )

        setContentView(root)
    }

    @SuppressLint("SetJavaScriptEnabled")
    private fun buildWebView(): WebView {
        val view = WebView(this)
        view.setBackgroundColor(Color.BLACK)
        view.settings.apply {
            javaScriptEnabled = true
            domStorageEnabled = true
            cacheMode = WebSettings.LOAD_NO_CACHE
            loadWithOverviewMode = true
            useWideViewPort = true
            allowFileAccessFromFileURLs = false
            allowUniversalAccessFromFileURLs = false
        }
        view.webViewClient = WebViewClient()
        view.overScrollMode = View.OVER_SCROLL_NEVER
        return view
    }

    private fun connect() {
        val raw = input.text.toString().trim()
        val host = raw.substringBefore(':').trim()
        val portText = if (raw.contains(':')) raw.substringAfter(':').trim() else "8090"
        val port = if (portText.isEmpty()) 8090 else portText.toIntOrNull()

        if (host.isEmpty() || port == null || port !in 1..65535) {
            Toast.makeText(this, R.string.invalid_address, Toast.LENGTH_SHORT).show()
            return
        }

        prefs.edit().putString(KEY_HOST, "$host:$port").apply()

        input.visibility = View.GONE
        connectButton.visibility = View.GONE
        webView.visibility = View.VISIBLE
        webView.loadUrl("file:///android_asset/www/index.html?host=$host&port=$port")
    }

    private fun showConnectScreen() {
        webView.loadUrl("about:blank")
        webView.visibility = View.GONE
        input.visibility = View.VISIBLE
        connectButton.visibility = View.VISIBLE
        val saved = prefs.getString(KEY_HOST, "") ?: ""
        if (input.text.toString() != saved) {
            input.setText(saved)
        }
    }

    private fun lp(width: Int, height: Int, left: Int = 0, top: Int = 0, right: Int = 0, bottom: Int = 0) =
        LinearLayout.LayoutParams(width, height).apply {
            setMargins(left, top, right, bottom)
        }

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()

    private companion object {
        const val PREFS_NAME = "phonedisplay"
        const val KEY_HOST = "host"
    }
}
import { APP_CONTAINER_CLASS, STATIC_PATH, WDS_PORT } from '../shared/config'
import { isProd } from '../shared/util'

const renderApp = title =>
`<!DOCTYPE html>
<html>
<head>
  <title>${title}</title>
  <meta charset="utf-8" />
  <link href="${STATIC_PATH}/style.css" rel="stylesheet"></link>
  <link rel="apple-touch-icon" sizes="180x180" href="${STATIC_PATH}/favicon/apple-touch-icon.png">
  <link rel="icon" type="image/png" href="${STATIC_PATH}/favicon/favicon-32x32.png" sizes="32x32">
  <link rel="icon" type="image/png" href="${STATIC_PATH}/favicon/favicon-16x16.png" sizes="16x16">
  <link rel="manifest" href="${STATIC_PATH}/favicon/manifest.json">
  <link rel="mask-icon" href="${STATIC_PATH}/favicon/safari-pinned-tab.svg" color="#ee9f20">
  <meta name="theme-color" content="#ffffff">
</head>
<body>
  <div class="${APP_CONTAINER_CLASS}">
    <header>
      <h1>${title}</h1>
    </header>
    <div class="content">
      <p>Block height: <span class="block-height"></span></p>
      <p>Denomination: <img src="${STATIC_PATH}/assets/btc-sans.png" alt="BTC" class="btc"/> <span class="denomination"></span></p>
      <p>Fee: <img src="${STATIC_PATH}/assets/btc-sans.png" alt="BTC" class="btc" /> <span class="fee"></span></p>
      <div class="cycles">
        <h2>Cycles</h2>
        <ul></ul>
      </div>
      <footer>
        2017 NTumbleBit
        &middot;
        <a href="http://github.com/NTumbleBit/NTumbleBit">http://github.com/NTumbleBit/NTumbleBit</a>
        &middot;
        <a href="http://github.com/TumbleBit/TumbleBit">http://github.com/TumbleBit/TumbleBit</a>
      </footer>
    </div>
  </div>
  <script src="${isProd ? STATIC_PATH : `http://localhost:${WDS_PORT}/dist`}/js/bundle.js"></script>
</body>
</html>
`

export default renderApp

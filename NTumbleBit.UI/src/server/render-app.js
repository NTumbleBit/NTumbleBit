import { APP_CONTAINER_CLASS, STATIC_PATH, WDS_PORT } from '../shared/config'
import { isProd } from '../shared/util'

const renderApp = title =>
`<!DOCTYPE html>
<html>
<head>
  <title>${title}</title>
  <meta charset="utf-8" />
  <link href="${STATIC_PATH}/style.css" rel="stylesheet"></link>
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

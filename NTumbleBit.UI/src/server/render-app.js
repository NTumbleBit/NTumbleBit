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
  </div>
  <script src="${isProd ? STATIC_PATH : `http://localhost:${WDS_PORT}/dist`}/js/bundle.js"></script>
</body>
</html>
`

export default renderApp

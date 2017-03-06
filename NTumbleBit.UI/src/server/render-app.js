import { STATIC_PATH } from '../shared/config'

const renderApp = title =>
`<!DOCTYPE html>
<html>
<head>
  <title>${title}</title>
  <meta charset="utf-8" />
  <link href="${STATIC_PATH}/style.css" rel="stylesheet"></link>
</head>
<body>
  <div class="wrap">
    <header>
      <h1>${title}</h1>
    </header>
  </div>
</body>
</html>
`

export default renderApp

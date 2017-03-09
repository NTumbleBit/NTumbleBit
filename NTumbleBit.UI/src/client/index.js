import 'babel-polyfill'
import 'isomorphic-fetch'

import { TUMBLER_SERVER_PORT } from '../shared/config'

const apiUri = `http://${window.location.hostname}:${TUMBLER_SERVER_PORT}/api`
const cycleStateUri = '/Tumbles'
const blockHeightUri = '/BlockHeight'
const feeUri = '/Fee'
const denominationUri = '/Denomination'

const SATOSHI_TO_BTC = 100000000

// utility ---------------------------------------------------------

function handleErrors(res) {
  if (!res.ok) {
    throw Error(res.statusText)
  }
  return res
}

// component generators ---------------------------------------------

function genAliceComponent(alice) {
  return `
    <li class='alice'>
      <h4>${alice.txId || 'unconfirmed'}</h4>
      <p>Status: ${alice.status}</p>
    </li>
  `
}

function genBobComponent(bob) {
  return `
    <li class='bob'>
      <h4>${bob.txId || 'unconfirmed'}</h4>
      <p>Status: ${bob.status}</p>
    </li>
  `
}

function genCycleComponent(cycle) {
  const height = cycle.cycle
  const aliceComponents = cycle.alices.map(alice => genAliceComponent(alice)).join('')
  const bobComponents = cycle.bobs.map(bob => genBobComponent(bob)).join('')

  return `
    <li class='cycle' id='cycle-${height}'>
      <h4 class='cycle-title'>Cycle ${height}</h4>
      <div class='channels'>
        <div class='alices flex'>
          <h5>Alices</h5>
          <ul>${aliceComponents}</ul>
        </div>
        <div class='bobs flex'>
          <h5>Bobs</h5>
          <ul>${bobComponents}</ul>
        </div>
      </div>
    </li>
  `
}

// Update Functions -------------------------------------------------

function updateDenomination(denom) {
  const denomDiv = document.querySelector('.denomination')
  // denomination given in satoshi
  denomDiv.textContent = (denom / SATOSHI_TO_BTC)
}

function updateFee(fee) {
  const feeDiv = document.querySelector('.fee')
  feeDiv.textContent = (fee / SATOSHI_TO_BTC)
}

function updateBlockHeight(height) {
  const blockHeightDiv = document.querySelector('.block-height')
  blockHeightDiv.textContent = height
}

function updateCycles(cycles) {
  const cyclesDiv = document.querySelector('.cycles ul')
  cyclesDiv.innerHTML = cycles.map(cycle => genCycleComponent(cycle)).join('')
}

// Fetch data ------------------------------------------------------

const getReqInit = {
  method: 'get',
  headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  mode: 'cors',
  cache: 'default',
}

function fetchBlockHeight() {
  fetch(apiUri + blockHeightUri, getReqInit)
    .then(handleErrors)
    .then(res => res.json())
    .then(height => updateBlockHeight(height))
}

function fetchEndpoints() {
  fetchBlockHeight()

  fetch(apiUri + cycleStateUri, getReqInit)
    .then(handleErrors)
    .then(res => res.json())
    .then(cycles => updateCycles(cycles))
}

(function poll() {
  fetchEndpoints()
  setTimeout(poll, 10000)
}())


// Denom & Fee only get called once
;(function fetchDenomination() {
  fetch(apiUri + denominationUri, getReqInit)
  .then(handleErrors)
    .then(res => res.json())
    .then(denom => updateDenomination(denom))
}())

;(function fetchFee() {
  fetch(apiUri + feeUri, getReqInit)
    .then(handleErrors)
    .then(res => res.json())
    .then(fee => updateFee(fee))
}())


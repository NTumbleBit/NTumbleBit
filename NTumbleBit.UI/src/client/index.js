import 'babel-polyfill'
import 'isomorphic-fetch'

import { TUMBLER_SERVER_PORT } from '../shared/config'

const apiUri = `http://${window.location.hostname}:${TUMBLER_SERVER_PORT}/api`
const cycleStateUri = '/Cycles'
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

function genCycleComponent(cycle) {
  const height = cycle.height
  return `
    <li class='cycle' id='cycle-${height}'>
      <h4>Cycle ${height}</h4>
      <div class='inputs flex'>
        <ul></ul>
      </div>
      <div class='outputs flex'>
        <ul></ul>
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

